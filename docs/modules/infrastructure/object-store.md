---
title: The photo object store
kind: reference
status: current
module: infrastructure
verified: 2026-08-30
code:
  - GSBC.ImpactKids.AppHost/AppHost.cs
  - Charts/impact-kids/templates/s3
  - Charts/impact-kids/values.yaml
---

# The photo object store

Person photos live in an S3-compatible object store, not in Postgres. One bucket, `photos`, reachable
only from inside the cluster. This doc is the store itself — how it is declared, how it authenticates,
and how it is backed up. What the application does with it is the gRPC service's business.

## SeaweedFS, and why not the two obvious alternatives

The store is [SeaweedFS](https://github.com/seaweedfs/seaweedfs) running `weed server -s3`: master,
volume, filer and the S3 gateway in a single process. For one bucket of roughly 100 MB, one small
container is the right size of thing.

**Not MinIO.** Its community edition had the admin UI stripped in mid-2025, went to maintenance mode
that December and was archived in early 2026 — no security patches, no pre-built binaries.

**Not Garage**, which is leaner, because its buckets and keys are created by running `garage key
create` against a live node. That means an imperative post-install Helm hook. SeaweedFS' S3 identities
are a JSON document it reads at startup, which is the deciding difference: the chart declares the
credentials and there is no ordering to get right.

The .NET side is `AWSSDK.S3` against a custom endpoint with `ForcePathStyle`, so swapping the store
later is an endpoint and two credentials.

## Nothing outside the cluster ever reaches it

There is no Ingress and no YARP route. The gRPC service is the only client, and photos reach the
browser through the API under the auth that already exists — the same rule, for the same reason, as
`PickupDisplayKeyEndpoints`' `internal/` group.

**Do not put a presigned URL in front of an `<img>`.** They work here, but they defeat browser
caching (a fresh signature every mint means the browser sees a new resource every time), they would
require exposing the store publicly, and a presigned URL is a bearer credential with no tie back to
the leader's session — weaker than what we have, not stronger. SeaweedFS also has a
[known `SignatureDoesNotMatch` bug](https://github.com/seaweedfs/seaweedfs/discussions/3976) when
presigning is combined with a static `-s3.config`, which is exactly the configuration below.

## Two identities, and one of them cannot write

`s3-identities-secret` is mounted at `/etc/seaweedfs/s3.json` and passed as `-s3.config`:

| Identity | Actions | Used by |
|---|---|---|
| `impact-kids` | `Read`, `Write`, `List`, `Tagging`, `Admin` | the gRPC service — the only writer |
| `backup` | `Read:photos`, `List:photos` | the hourly rclone CronJob |

The split is the point: the backup job holds a credential that cannot delete the thing it exists to
protect. Verified 2026-08-29 against `chrislusf/seaweedfs:3.98` — with this document the `backup`
identity gets 200 on GET and LIST and **403 on PUT and DELETE**, and an unsigned request gets 403.

### The three Secrets are created by hand, and the chart only references them

Same rule as `sql-secrets`, `rabbitmq-secrets` and `grpc-secrets`. **The chart renders no Secret and
`values.yaml` has no `secrets:` block**, because values files live in the Argo repo and that is git.
An earlier draft assembled the identities document from Helm values; it was changed for exactly this
reason.

Make all three once per environment.

**Where the values come from is the part that catches people.** There are two kinds:

| Value | Origin |
|---|---|
| `APP_KEY`, `APP_SECRET`, `BACKUP_KEY`, `BACKUP_SECRET` | **Invented.** SeaweedFS has no key-issuing step — whatever is written into `s3.json` *becomes* the valid credential and nothing else authenticates. Generate opaque strings (`openssl rand -hex 24`) and avoid punctuation; they are signed into S3 headers and pasted into shell and JSON by hand. |
| `B2_KEY_ID`, `B2_APPLICATION_KEY` | **Issued by Backblaze** when an application key is created in the B2 console. Not chosen. |

Secret (3) mixes the two: its `SEAWEED` half is the invented backup pair, its `B2` half is
Backblaze's. Nothing validates that the copies agree — a mismatch is a 403 at runtime, not an
install error.

```bash
# 1. SeaweedFS' own identities document. Two identities: the app, and a read-only backup reader.
cat > /tmp/s3.json <<'JSON'
{
  "identities": [
    { "name": "impact-kids",
      "credentials": [ { "accessKey": "APP_KEY", "secretKey": "APP_SECRET" } ],
      "actions": [ "Read", "Write", "List", "Tagging", "Admin" ] },
    { "name": "backup",
      "credentials": [ { "accessKey": "BACKUP_KEY", "secretKey": "BACKUP_SECRET" } ],
      "actions": [ "Read:photos", "List:photos" ] }
  ]
}
JSON
kubectl -n impact-kids create secret generic s3-identities-secret \
  --from-file=s3.json=/tmp/s3.json
rm /tmp/s3.json

# 2. The same app credential, in the shape .NET configuration reads. Consumed by the gRPC service
#    and the backfill worker.
kubectl -n impact-kids create secret generic photos-secret \
  --from-literal=Photos__AccessKey=APP_KEY \
  --from-literal=Photos__SecretKey=APP_SECRET

# 3. The backup job's two ends. SEAWEED is the source and is read-only; B2 is the destination and
#    must be able to write. See "The two ends need different permissions" below.
#    Only needed when backup.s3.enabled is true.
kubectl -n impact-kids create secret generic s3-backup-secret \
  --from-literal=RCLONE_CONFIG_SEAWEED_ACCESS_KEY_ID=BACKUP_KEY \
  --from-literal=RCLONE_CONFIG_SEAWEED_SECRET_ACCESS_KEY=BACKUP_SECRET \
  --from-literal=RCLONE_CONFIG_B2_ACCESS_KEY_ID=B2_KEY_ID \
  --from-literal=RCLONE_CONFIG_B2_SECRET_ACCESS_KEY=B2_APPLICATION_KEY
```

`APP_KEY`/`APP_SECRET` must be identical in (1) and (2), and `BACKUP_KEY`/`BACKUP_SECRET` identical
in (1) and (3). They are the same credentials expressed in two formats — SeaweedFS wants its own
JSON, the clients want environment variables. The Backblaze pair appears only in (3).

## The volume flags are load-bearing, not tuning

Both the chart and `AppHost.cs` pass:

```
-master.volumeSizeLimitMB=128 -master.volumePreallocate=false -volume.max=8
```

**Do not drop them back to the defaults.** `weed server` allocates volume files of 1 GB each and
grows them seven at a time, so storing three small objects claimed **7 GB of disk** — measured
2026-08-29, and it filled the Docker VM outright. With these flags the same three objects take
236 KB.

`volume_size_limit_mb × volume_max` is the hard ceiling on what SeaweedFS will allocate: 1 GB, about
four times the ten-year estimate. The PVC is 2 Gi so it stays comfortably above that ceiling — the
volume files are not alone on it, the filer's leveldb store shares the same directory.

The symptom when this is wrong is not an out-of-space error from the API. It is `400 InvalidRequest`
on every object PUT, with `No more free space left` and `failing to assign a file id` only in the
container's log.

## Changing an identity does not roll the pod

SeaweedFS reads `-s3.config` once at startup, and because the Secret is created out of band there is
no rendered checksum for the chart to hang a pod annotation on. So a changed identity updates the
Secret in place while the running process keeps serving the keys it already read — no error, no sign
of disagreement.

**Follow any identity change with `kubectl -n impact-kids rollout restart statefulset/s3-statefulset`**, and update
`photos-secret` in the same sitting or the app is left holding a credential the store no longer
accepts.

## Local development

`AppHost.cs` declares the same image as a container with a data volume and
`ContainerLifetime.Persistent`, on `http://localhost:60537`, continuing the 60535/60536 series.

Local dev configures the credential differently from the cluster, and deliberately: it sets
`AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`, which SeaweedFS reads at startup to seed a single
admin identity, instead of mounting an `s3.json`. There is no file to generate and the Aspire
parameters flow straight in. Both mechanisms are declarative and read at startup; the cluster needs
the file form only because it needs the second, read-only identity.

- `s3-access-key` — a plain parameter, value `impact-kids`.
- `s3-secret-key` — generated once and kept in the AppHost's user secrets, like `sql-password`. No
  special characters: the value is signed into S3 request headers and pasted into shell and YAML by
  hand often enough that a quoting mistake is the likelier failure than a short alphabet.

**Anonymous access is refused.** Verified 2026-08-29: with those environment variables set, an
unsigned request to the S3 port returns 403 `AccessDenied`.

### It is the one persistent container a regenerated password cannot break

[Generated passwords and persistent volumes](generated-passwords.md) documents a failure that this
container's shape — persistent lifetime plus a data volume — normally invites. **SeaweedFS does not
have it.** Postgres and RabbitMQ seal their password into the data directory the first time they
initialise; SeaweedFS holds nothing about its S3 identity on the volume and re-reads it from the
environment at every start.

Verified 2026-08-29 by writing an object, restarting onto the same `/data` with a different secret,
and reading the object back with the new credential while the old one was refused with
`SignatureDoesNotMatch`.

So a regenerated `s3-secret-key` costs nothing here. The volume still holds real photos, so it is not
a thing to delete casually — it just cannot lock you out.

## Offsite backup — hourly `rclone copy` to Backblaze

A `CronJob` on the `rclone/rclone` image, hourly:

```
rclone copy seaweed:photos b2:<bucket> --size-only --immutable
```

**`copy`, never `sync`.** `copy` does not delete at the destination, so a bad migration or a
fat-fingered bulk delete on our side cannot erase the offsite copy. Changing that one word turns the
backup into a mirror of the mistake.

- `--immutable` fails the run loudly if an object's content ever changes under a name that should be
  a content hash. That is a bug in the application, and this is where it surfaces.
- `--size-only` because keys are content hashes and objects are never rewritten — reconciliation is a
  list-versus-list, so hashing and timestamp comparison buy nothing.
- `concurrencyPolicy: Forbid`, so a slow run cannot stack up behind itself.

rclone's whole remote definition comes from `RCLONE_CONFIG_<REMOTE>_<OPTION>` environment variables,
so there is no `rclone.conf` to mount. Non-secret options sit on the CronJob; the four credentials
come from `s3-backup-secret`.

### The two ends need different permissions

Easy to conflate, because both live in the same Secret and the job is called "backup":

| Remote | Credential | Needs | Must NOT have |
|---|---|---|---|
| `seaweed:` — the source | the SeaweedFS `backup` identity | `Read`, `List` | write, delete |
| `b2:` — the destination | a Backblaze application key | list files, **write files** | **delete files** |

The source credential is read-only because rclone only ever reads there; that is what stops a
compromised or mis-typed backup job damaging the live bucket it exists to protect.

The destination credential obviously has to write, or there would be no backup. **Do not give it
delete.** `rclone copy` never issues a delete, so the capability is unused in normal operation — and
withholding it means that neither end of this job can destroy the offsite copy, which together with
B2's default versioning makes the backup effectively append-only. Add `readFiles` as well if you want
the same key to serve a restore; otherwise mint a separate one when you need it.

The job is **disabled by default** (`backup.s3.enabled`). The Backblaze bucket and endpoint are
`required` when it is enabled, so a half-configured install fails at `helm upgrade` rather than
running an hourly job that silently copies nothing. The credentials are not values at all — they are
in `s3-backup-secret`, created by hand.

### Why not SeaweedFS' built-in `filer.backup`

It looks like the obvious choice — same binary, Backblaze is a named sink, near real-time — and
[discussion #8672](https://github.com/seaweedfs/seaweedfs/discussions/8672) is why it is not.

The disqualifying issue is that **its progress checkpoint lives in the source filer, not the
destination.** Emptying or recreating the Backblaze bucket does not reset it: the daemon carries on
from where it was and the destination silently holds only what was written since. That is the failure
mode where you believe you have a backup and do not. It is also replication rather than backup — it
must run continually and is only eventually consistent — and it has no point-in-time recovery.

`rclone` derives what to send from the two bucket listings, so there is no checkpoint to drift.

**The filer-metadata complaint in that thread does not reach us, and it matters why.** rclone reads
through the S3 API, so what lands at Backblaze is plain objects at plain keys, not volume files that
need a filer store to say which chunks belong to which name. Restoring is byte-for-byte the same
operation the application performs every time a leader takes a photo: a PUT through the S3 API. There
is no metadata that can be lost, because none of it is an input to the restore — it is an output.

That complaint becomes ours the moment anyone backs up the PVC directly instead. Then the bytes are
meaningless without the filer store that indexes them, and it needs `filer.meta.backup` plus a story
for the consistency gap between two snapshots. Backing up at the S3 layer is what avoids all of it.

### Cost, and the one thing that grows

Sizing from the real roll — about 340 children at 500×500 JPEG q0.85, so roughly 35 KB each:

| | Objects | Size |
|---|---|---|
| One current photo per child | ~340 | ~12 MB |
| Growth at ~2 re-shoots per child per year | ~680/yr | ~24 MB/yr |
| Ten years, nothing ever pruned | ~7,000 | ~250 MB |

Against B2's permanent 10 GB free tier that is free and stays free for decades. An hourly run lists
both sides — about 48 calls a day against a free allowance of 2,500.

Because the job uses `copy` and not `sync`, **a superseded photo is never removed from Backblaze.**
That is deliberate: it is what makes an accidental bulk delete survivable, and at these sizes the
accumulation is a free photo history rather than a cost. If it ever does need pruning, reconcile the
bucket against the `PhotoVersion` values in the database and delete what nothing references.

**Do not reach for a date-based lifecycle rule.** B2 buckets are versioned by default; leave that on.
If a lifecycle rule is ever added it must be the *"keep prior versions for N days"* kind. Because keys
are content hashes nothing is ever rewritten, so a plain age-based expiration rule would eventually
match every current photo and quietly delete the entire backup.

Restore is deliberately dull — `rclone copy` the other way. Worth rehearsing once on the dev stack
before anyone needs it.

## The chart is hand-written

`Charts/impact-kids/` is hand-maintained and is what gets deployed. It is **not** the output of
`aspire publish` — that writes `k8s-artifacts/`, which is untracked, unreferenced and has already
diverged in shape. Do not run the publisher and do not treat its output as the chart.
