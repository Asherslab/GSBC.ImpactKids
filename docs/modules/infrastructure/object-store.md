---
title: The photo object store
kind: reference
status: current
module: infrastructure
verified: 2026-08-29
code:
  - GSBC.ImpactKids.AppHost/AppHost.cs
  - Charts/impact-kids/templates/s3
  - Charts/impact-kids/values.yaml
---

# The photo object store

Person photos live in an S3-compatible object store, not in Postgres. One bucket, `photos`, reachable
only from inside the cluster. This doc is the store itself — how it is declared and how it
authenticates. What the application does with it is the gRPC service's business.

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

The chart renders `s3-identities-secret`, mounted at `/etc/seaweedfs/s3.json` and passed as
`-s3.config`:

| Identity | Actions | Used by |
|---|---|---|
| `impact-kids` | `Read`, `Write`, `List`, `Tagging`, `Admin` | the gRPC service — the only writer |
| `backup` | `Read:photos`, `List:photos` | the offsite backup job — **not landed yet** |

The split is the point: the backup job holds a credential that cannot delete the thing it exists to
protect. Verified 2026-08-29 against `chrislusf/seaweedfs:3.98` — with this document the `backup`
identity gets 200 on GET and LIST and **403 on PUT and DELETE**, and an unsigned request gets 403.

Both credentials are `required` in the chart and blank in `values.yaml`. Supply them at install time.
An install that leaves one blank fails at `helm upgrade`, which is the intended behaviour: an object
store whose access key is the empty string is worse than a failed install.

This differs from `sql-secrets` and `rabbitmq-secrets`, which are created out of band with `kubectl`
and merely referenced by the chart. The s3 credentials are not loose environment variables — they are
a structured identities document the chart has to assemble — so the chart owns the Secret.

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

## A rolled Secret must roll the pod

The StatefulSet carries a `checksum/identities` pod annotation over the rendered Secret. Without it,
changing a key updates the Secret in place while the running process keeps serving the identities it
read at startup — the chart and the store then disagree with nothing to show for it.

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

## The chart is hand-written

`Charts/impact-kids/` is hand-maintained and is what gets deployed. It is **not** the output of
`aspire publish` — that writes `k8s-artifacts/`, which is untracked, unreferenced and has already
diverged in shape. Do not run the publisher and do not treat its output as the chart.
