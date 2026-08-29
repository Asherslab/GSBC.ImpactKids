---
title: Gender, person photos, and the photos tool
kind: plan
status: folded
closed: 2026-08-29
folded_into:
  - docs/modules/people/gender.md
  - docs/modules/people/photos.md
  - docs/modules/infrastructure/object-store.md
  - docs/modules/elvanto/api-reference.md
module: people
opened: 2026-08-29
verified: 2026-08-29
code:
  - GSBC.ImpactKids.Grpc/Features/People/Sync/Descriptors
  - GSBC.ImpactKids.Grpc/Features/Elvanto/ElvantoServices
  - GSBC.ImpactKids.Grpc/Data/Models/People/DbPerson.cs
  - GSBC.ImpactKids.WASM/Features/People/Components/Individual
  - GSBC.ImpactKids.WASM/Features/Attendance/Pages
  - GSBC.ImpactKids.AppHost/AppHost.cs
  - Charts/impact-kids
---

# Gender, person photos, and the photos tool

**Executing this?** [The execution handover](2026-08-gender-and-person-photos-execution.md) is the how — order of work, what to parallelise, and what to stop and ask for. This doc is the what and the why.

Three related pieces of work: a `Gender` field synced from Elvanto and enforced at sign in, photo
storage in an in-cluster S3, and a Photos tool that lets a leader take a child's photo on a phone
during a service.

## Where the Elvanto API is documented

Bookmark for every future session. The docs are thin and occasionally wrong, so measured behaviour
is recorded beside each claim below.

| Page | URL |
|---|---|
| People index — every endpoint | <https://www.elvanto.com/api/people/> |
| `people/getAll` — fields and filters | <https://www.elvanto.com/api/people/getAll/> |
| `people/getInfo` | <https://www.elvanto.com/api/people/getInfo/> |
| `people/create` | <https://www.elvanto.com/api/people/create/> |
| `people/edit` | <https://www.elvanto.com/api/people/edit/> |
| `people/customFields/getAll` | <https://www.elvanto.com/api/people/customFields/getAll/> |
| People fields reference | <https://www.elvanto.com/api/people-fields/> |
| Services | <https://www.elvanto.com/api/services/> |

The account's own quirks already learned the hard way live in
`ElvantoPersonFields` and `SchoolGradeDescriptor` — read those before trusting a doc page.

## What the API actually returns

Probed read-only against the live account on 2026-08-29, `people/getAll` page 1 of 1754 people,
`page_size: 1000`.

**`gender`**

- **Correction, measured 2026-08-29 while building A3:** it is **not** returned by default. It has
  to be named in the `fields` array. A `getAll` without it omits the key entirely, with or without a
  `fields` array. This doc originally said the opposite; `GenderDescriptor` was built to that claim
  and read null for all 1735 people, writing zero inbound rows while every part of the sync looked
  healthy. See [the API reference](../modules/elvanto/api-reference.md#gender).
- Values are exactly `"Male"`, `"Female"` and `""`. No other value appeared.
- Listed under `fields` on `people/create` and `people/edit`, so it is writable.
- **Coverage is poor.** Across the 1000: 102 Male / 102 Female / 96 blank in the first 300; over
  the 195 people who have a `school_grade` (i.e. the children this app signs in) it is
  69 Male / 57 Female / **69 blank — 35%**.

**`picture`**

- Returned on every person by default, and **rejected if asked for in `fields`**:
  `code 250: A field does not exist (picture)`. Do not add it to the `Fields` array in
  `ElvantoService.FetchPageWithRetries` — the whole call fails.
- Three URL shapes, and only one is a real photo:

  | Shape | Meaning | Share of 1000 |
  |---|---|---|
  | `cdn.elvanto.com.au/img/default-avatar.svg` | no photo | 330 |
  | `secure.gravatar.com/avatar/…?d=…default-member-avatar.png` | no photo, gravatar fallback | 412 |
  | `d2dek0x2lg6bxh.cloudfront.net/VY9YW40G/members/<id>_thumb_<unix-ts>.jpg` | **a real upload** | 258 |

- Real photos are 500×500 JPEG, 7–53 KB. Already the size we want; no re-encoding needed on ingest.
- **57% of the real URLs are malformed by Elvanto** — the thumb suffix has a whole second URL
  concatenated into it (`…_thumb_https://d1o7yryu40l3o2.cloudfront.net/…`), a botched migration on
  their side. Those 403 permanently, and `people/getInfo` returns the identical broken URL, so
  there is no repair path. Sampled fetches: 25 × 200, 15 × 403.
- **There is no way to write a picture.** `people/edit` and `people/create` list no `picture`
  parameter at top level or under `fields`. Decided: the pull is a one-off backfill and the app
  owns photos from then on. See "Export for Elvanto staff" for the agreed substitute.

**The number that shapes the work:** of ~195 children per 1000 people, 79 have any Elvanto photo and
roughly 40% of those are dead links. **About 75–80% of children will need a photo taken in the app
regardless.** The Elvanto backfill is a head start, not the solution — build the capture tool first.

## A. Gender

A field like any other, and the sync engine already has the shape for it.

### Slice A1 — the field, end to end

- `DbPerson.Gender` — `string?`, matching how `MediaConsent` is stored.
- `Person.Gender` — `Gender?` enum `{ Male, Female }` on the contract. **Null is the third state and
  it is a real answer**, so it is a nullable enum rather than a `Gender.Unknown` member; an
  `Unknown` member invites code that treats it as a value and pushes it to Elvanto.
- `CreatePersonRequest.Gender`, `UpdatePersonRequest.Gender` as `DeltaUpdate<Gender?>`, wired into
  `FromEntity`.
- Additive migration, one nullable column. No backfill.
- UI: a `MudSelectCreateOrUpdate` in `PersonImpactDetails.razor`, directly under Media Consent, with
  the same `ErrorFunc` treatment. Verified on the running app 2026-08-29: that card is the narrow
  left column of the person page and of the sign-in "Check Details" step, and already stacks
  First Time / Media Consent / School Grade — this is a fourth item in the same stack, no layout
  change.

Guardians see no gender field, because `PersonImpactDetails` only renders for non-guardians —
`PersonDetails.razor` switches to `PersonContactDetails` when `FamilyGuardian` is set. The sync
still stores gender for guardians; it simply is not shown or enforced. That is the right split:
the error exists to stop a child being signed in against an incomplete profile.

### Slice A2 — the sign-in error

`PersonImpactDetails.razor.cs` already carries `_mediaConsentError` and `_firstTimeError` and ORs
them into `SendErrorsChanged`. Add `_genderError`, true when the value is null, using the same
`ErrorFunc` → `SendErrorsChanged` pattern. It flows through `PersonDetails` →
`PersonOverview.PersonDetailsErrorsChanged` → `SignIn.razor`'s `_errorsInPerson`, which reddens the
"Check Details" step and shows the existing alert. No new plumbing.

**Expect this to fire on roughly a third of children on the first night.** 35% of children with a
school grade have no gender in Elvanto. That is the point of the change, but it is worth telling
leaders before it lands rather than after.

### Slice A3 — the sync descriptor

`GenderDescriptor : BaseFieldSyncDescriptor`, a near copy of `MediaConsentDescriptor`. Descriptors
auto-register — `ServiceExtensions.AddPeopleSync` scans for `IFieldSyncDescriptor` — so there is no
registration step.

- `FieldName` = `"Gender"`, `DefaultDirection` = `Bidirectional`.
- `GetFromElvanto` returns `elv.Gender` verbatim; `""` falls to the base `IsValidInboundValue`,
  which already refuses blank. Elvanto holding nothing must never clear a leader's entry.
- `ApplyToElvantoRequest` sets `fields.gender` to `"Male"` / `"Female"`, refusing anything else —
  the same "declining is correct, reporting a decline as a push is not" rule Media Consent follows.
- `ElvantoPerson.Gender` (`[JsonPropertyName("gender")]`, plain `string?`) and
  `ElvantoPersonFields.Gender`. **`"gender"` must be added to the requested `Fields` array** —
  corrected from this doc's original instruction to leave it out, which was wrong and produced a
  silently dead field. `picture` is the field that must never be named there.
- `ElvantoService.GetPeople` and `CreatePerson` carry gender too.

**"Take Elvanto's value when the app holds null" needs no special casing — it is already what the
reconciler does**, and tracing it is worth doing once because it also settles how much outbound
traffic this field creates.

At seed time every app-side gender is null, so `FieldReconciler.Decide` sees no base row and falls
to `DecideFirstSync`, where two branches cover the whole roll:

| App | Elvanto | Branch | Result |
|---|---|---|---|
| `null` | `"Male"` | `FirstSync:ElvantoPrecedence` | **Inbound** — Elvanto's value lands |
| `null` | `""` | `Match:NeitherSideSaysAnything` | **Agreed** — base settled, no row, no noise |

`FirstSyncPrecedence` already defaults to `SyncSource.Elvanto`, and `appHasSomethingToSay` is false
for every person, so the `FirstSync:AppPrecedence` and `FirstSync:ElvantoHasNothing` branches — the
two that produce outbound rows — are unreachable on the first run. **A `Bidirectional` gender
descriptor therefore plans zero outbound writes at seed time.**

Outbound rows appear later and only from one source: a leader filling in a gender for a child
Elvanto has blank, which then reads as an app-side change against the settled base. That is the
correct end state and the reason the sync is bidirectional, so it is a feature rather than a leak.

The one obligation it creates is a reporting one, not a blocking one: whoever next runs the gate in
[the write-testing handover](../work/2026-08-elvanto-write-testing.md) will see gender rows in the plan and
must approve them knowingly. That is exactly what the gate is for — it is a human approval of
whatever plan is about to execute, not a prohibition on the plan gaining fields — and `AllowWrites`
is off, so nothing can leave in the meantime. Ship it `Bidirectional`.

**There are tests, and they cover this for free.** `GSBC.ImpactKids.Grpc.Tests/Sync/` holds
`FieldNameParityTests`, which reflects over every `IFieldSyncDescriptor` in the Grpc assembly and
asserts each `FieldName` matches an EF property on `DbPerson` — because
`FieldChangeTrackingInterceptor` writes EF's property name into `FieldChangeLogs`, and a mismatch
would break the field's sync silently rather than loudly. So `GenderDescriptor.FieldName` must be
exactly `"Gender"` and `DbPerson.Gender` must exist, or the build goes red. That is a gift: it means
A1 has to land before or with A3, and the test says so without anyone remembering to check. The
descriptor must also be constructible with no arguments, as the others are.

Note that **the root `AGENTS.md` claim that "this repo has no test projects" is stale** — worth
fixing while in here, because it currently tells a reader not to look for the safety net that exists.

## B. Photos — storage

### The decision

An **in-cluster S3-compatible object store**, one bucket, reachable only from inside the cluster.
Chosen over Postgres `bytea` because the existing backup software already ships to a Backblaze S3
bucket, so backups come almost free and blobs stay out of the `pg_dump` that `db-restore.sh` pulls
down for local dev.

**Not MinIO.** MinIO's community edition had its admin UI stripped in mid-2025, went to maintenance
mode in December 2025 and was **archived in early 2026** — no security patches and no pre-built
binaries. It is not a safe thing to start a new deployment on. Sources:
[Blocks & Files](https://blocksandfiles.com/2025/06/19/minio-removes-management-features-from-basic-community-edition-object-storage-code/),
[Linuxiac](https://linuxiac.com/minio-steering-users-toward-paid-subscriptions/).

**Recommended: SeaweedFS** (Apache 2.0, actively maintained, picked up by Kubeflow Pipelines as
MinIO's replacement). One container in `server -s3` mode, and — the deciding factor — its S3
identities are **declarative**: a `-s3.config` JSON of access keys mounted from a Secret. Garage is
the leaner Rust alternative but needs `garage key create` / `garage bucket create` run against a
live node, i.e. an imperative post-install Helm hook. For one bucket of ~100 MB that machinery is
not worth it.

Either way the .NET side is `AWSSDK.S3` against a custom endpoint with `ForcePathStyle`, so the
store can be swapped later by changing an endpoint and two credentials.

### On presigned URLs — supported, but we should not use them

They work in SeaweedFS and Garage alike. We should still **not** put one in front of an `<img>`:

1. **They defeat browser caching.** A presigned URL carries a signature and expiry in the query
   string and is different every time it is minted, so the browser treats each one as a new
   resource. The explicit requirement here is that photos ride ordinary browser caching.
2. **They need a new public surface.** The browser would have to reach the object store directly,
   so it needs an ingress route. Today nothing but YARP is externally reachable, and that is worth
   keeping.
3. **They are weaker, not stronger.** A presigned URL is a bearer credential: once minted it works
   for anyone holding it, for its whole lifetime, with no tie back to the leader's session.
4. SeaweedFS additionally has a
   [known `SignatureDoesNotMatch` bug](https://github.com/seaweedfs/seaweedfs/discussions/3976)
   when presigning is combined with a static `-s3.config` — which is exactly the config we want.

So: **the object store stays cluster-internal with no ingress, and the gRPC service is its only
client.** Photos reach the browser through the API, under the auth that already exists.

### Slice B1 — the store

- `AppHost.cs`: `builder.AddContainer("s3", "chrislusf/seaweedfs")` with `server -s3`, a data volume
  and `ContainerLifetime.Persistent`, mirroring how `sql` and `rabbitmq` are declared. Access key
  and secret as Aspire parameters so they land in user secrets, like the other generated passwords —
  and read [generated passwords and persistent volumes](../modules/infrastructure/generated-passwords.md)
  first, because a data volume plus a regenerated credential is exactly the failure documented there.
- `Charts/impact-kids/templates/s3/` follows from `aspire publish`; the PVC pattern is already in
  the chart for `sql` and `rabbitmq`.
- **No ingress, no YARP route.** Same rule, and for the same reason, as
  `PickupDisplayKeyEndpoints`' `internal/` group.
### Slice B1a — offsite backup to Backblaze

**Use an hourly `rclone copy` CronJob, not SeaweedFS's built-in `filer.backup`.**

The built-in looks like the obvious choice — same binary, Backblaze B2 is a named sink, near
real-time. [Discussion #8672](https://github.com/seaweedfs/seaweedfs/discussions/8672) is why it is
not, and it is worth reading before anyone tries to "simplify" this back to the built-in:

- **It is replication, not backup.** In the original poster's words, *"You have to run the process
  continually and can't schedule regular backups. The process is only eventually consistent."*
- **The checkpoint lives on the source, not the destination.** Progress is an offset in the source
  filer's KV store, so **emptying or recreating the Backblaze bucket does not reset it** — the
  daemon carries on from where it was and the destination silently holds only what was written
  since. That is the failure mode where you believe you have a backup and do not. The escape hatch
  is `-timeAgo=876000h` to force a full re-sync, which the thread reports *sometimes doesn't work
  as expected*.
- **There is no point-in-time recovery.** A PITR feature existed and was removed for lack of demand;
  the maintainer's suggested mitigation is destination-side versioning.

So the disqualifying issue is the silent checkpoint drift. And the fix for it is a mechanism that
derives truth from the two buckets instead of from a stored offset — at which point the daemon is
no longer earning its place.

#### The filer-metadata complaint does not apply here

The thread also says `filer.meta.backup` must be run alongside, and that synchronising the two is
underdocumented and inconsistent. That one is real, and it is the reason to be careful about *what
layer* a backup is taken at — but it does not reach this design, and the reason is worth stating
plainly because it is the thing that makes the whole approach safe:

**We never back up SeaweedFS's internal representation, so we never need its metadata to read the
backup.** `rclone` reads through the **S3 API**, so what lands in Backblaze is plain objects at
plain keys — not volume files that need a filer store to say which chunks belong to which name.

The restore argument is stronger still: **restoring is byte-for-byte the same operation the app
performs every time a leader takes a photo.** The app stores a photo by PUTting an object through
the S3 API; a restore PUTs the same object through the same API. If a PUT could not reconstruct
everything needed to GET that object back, the app could not store a photo in the first place.
There is therefore no metadata that can be lost, because none of it is an input to the restore —
it is an output of it.

The metadata problem belongs to volume-level backup — copying `/data` off the PVC, or the older
`weed backup` path — where the bytes are meaningless without the filer store that indexes them.
**If anyone later proposes backing up the PVC directly instead, that is the moment this complaint
becomes ours**, and they would need `filer.meta.backup` and a story for the consistency gap between
the two snapshots. Backing up at the S3 layer is what avoids all of it.

The one genuinely filer-side thing worth naming: the S3 access keys live in the declarative
`-s3.config` Secret in the Helm chart, not in the filer, so they are already covered by whatever
backs up the cluster config. Nothing else about the bucket is state we would miss.

**This corrects the earlier recommendation in this doc's first draft**, which was `filer.backup`
with `is_incremental = true`. That was wrong on a second count as well: incremental mode files
objects into `YYYY-MM-DD` directories by modification date, so the destination is not a directly
restorable mirror — you would reassemble it by walking dates newest-first. `rclone copy` gives the
same "never propagate a delete" protection *and* keeps a flat mirror you can copy straight back.

**What to run instead.** The data suits this unusually well: a few hundred objects, ~100 MB total,
**named by content hash and never mutated**. So reconciliation is a list-versus-list and an upload of
the difference — no hashing, no timestamp comparison, no content diffing.

- A `CronJob` on the `rclone/rclone` image, hourly, with both endpoints from a Secret.
- `rclone copy seaweed:photos b2:impactkids-photos-backup --size-only --immutable`.
  **`copy`, never `sync`** — `copy` never deletes at the destination, so a bad migration or a
  fat-fingered bulk delete cannot erase the offsite copy. `--immutable` fails loudly if an object's
  content ever changes under a name that should be a content hash, which would mean a real bug.
- Hourly is well inside any sane RPO here: photos are written a handful at a time on a service
  night, and the worst case is re-shooting one child.
- Cost is negligible — a list of a few hundred keys per hour.

**On versioning.** B2 buckets are
[versioned by default and keep every version](https://www.backblaze.com/docs/cloud-storage-s3-compatible-api-bucket-versions).
Leave that on, but do not expect much from it here: because keys are content hashes there are
effectively **no overwrites**, so no versions accumulate. It earns its place only for the unlikely
case of something deleting objects on the B2 side directly — a mis-typed `rclone sync`, a console
delete — where the delete marker can be rolled back.

**If you ever add a lifecycle rule, it must be the "keep prior versions for N days" kind.** A plain
expiration rule deletes objects by age, and since these objects are never rewritten, every current
photo eventually becomes old enough to qualify. That rule would quietly delete the entire backup.
[B2 supports both through the S3-compatible lifecycle API](https://www.backblaze.com/blog/lifecycle-rules-now-supported-through-s3-compatible-apis/),
so the wrong one is easy to reach for.

#### What this costs at Backblaze — nothing, and it cannot run away

The design never sends a full copy. `rclone copy` compares the two listings and uploads only what
is missing, so a service night with twelve photos taken uploads twelve objects, and a day with none
uploads zero bytes.

Sizing from the real roll — about 340 children (195 with a `school_grade` per 1000 people, across
1754), at 500×500 JPEG q0.85, so 20–50 KB each, call it 35 KB:

| | Objects | Size |
|---|---|---|
| One current photo per child | ~340 | **~12 MB** |
| Growth, at ~2 re-shoots per child per year | ~680/yr | **~24 MB/yr** |
| After ten years, nothing ever pruned | ~7,000 | **~250 MB** |

Against [B2's permanent 10 GB free tier](https://www.backblaze.com/cloud-storage/pricing), that is
**free, and stays free for decades.** Beyond the free tier it is $0.00695/GB/month, so even the
ten-year figure would be under two cents a month.

Transactions are the other thing that can surprise people, and they do not here: an hourly run
lists both sides, roughly two calls per run at these object counts, so about 48 a day against a
free allowance of 2,500. Egress is free up to 3× stored size, and is only touched on a restore.

**The one real growth vector, named so nobody is surprised by it:** because the job uses `copy` and
not `sync`, a photo that has been superseded is never removed from Backblaze. That is deliberate —
it is what makes an accidental bulk delete survivable — and at these sizes the accumulation is a
free photo history rather than a cost. If it ever does need pruning, reconcile the bucket against
the `PhotoVersion` values in the database and delete what nothing references; do not reach for a
date-based lifecycle rule, for the reason above.

Restore is deliberately dull: `rclone copy` the other way, and the objects go back in through the S3
API. Worth actually rehearsing once, on the dev stack, before anyone needs it.

This is separate from the existing Postgres-dump backup, which will not pick up a new object store
on its own. Stand it up before the first photo is taken.

### Slice B2 — serving a photo

`GET /api/people/{id}/photo?v={hash}` — a minimal API endpoint in the gRPC service, in the idiom of
`AddEventEndpoints` / `AddPickupDisplayKeyEndpoints`. `/api/{**catch-all}` already routes there
through YARP under the `LeaderOrDisplay` policy, and the cookie the browser holds is attached
automatically to an `<img>` request on the same origin, so **no JS, no token plumbing, no fetch
wrapper**. The service's own fallback policy (`EnabledOnly`) makes it leader-only unless
deliberately marked otherwise.

- `Person.PhotoVersion` — a short content hash, or null when there is no photo. **A token on the
  contract, never the bytes.** Photos are deliberately outside the WASM store layer: 1772 people is
  far too much to cache in app state, and this keeps the store carrying a few bytes per person.
- Response: `image/jpeg`, `Cache-Control: private, max-age=31536000, immutable`. Because the version
  is in the URL, a re-shot photo is a *different* URL and busts itself — no revalidation, no ETag
  round trip, no stale face. Objects in the bucket are keyed by content hash, so they are immutable
  too and a re-shoot writes a new object rather than overwriting one.
- 404 when there is no photo, and the client must handle that as normal.

### Slice B3 — `PersonAvatar`, and never showing a broken image

One component, used everywhere a face appears, replacing the bare `MudAvatar` in
`PersonDisplay.razor` (verified on the running app: a rounded square with the first initial, left of
the name — the photo drops straight into it) and adding one to the person page's Details card.

The rule is that **the initial is the component, and the photo is an enhancement painted over it**:

- Render the coloured initial avatar always. It is the ground state, not a fallback branch.
- Emit an `<img>` only when `PhotoVersion` is non-null, absolutely positioned over the initial.
- `onerror` sets the `<img>` to `hidden`, revealing the initial underneath. No broken-image glyph,
  no alt text box, no layout shift — the element occupies no space either way.
- `onload` fades it in, so a slow photo never flashes.
- `loading="lazy"` and `decoding="async"`, since a list can hold 15 of these.

A 404, a dropped connection, an offline phone, an object the store has lost — every one of them
lands on the same path and the card looks exactly as it does today. Scoped CSS must reach in with
`::deep` from a plain wrapper, per the existing comment at the top of `PersonDisplay.razor`.

### Slice B4 — the backfill, as a job worker

A new `GSBC.ImpactKids.Workers.PhotoBackfill`, modelled on `GSBC.ImpactKids.Workers.DbMigrations`
(worker SDK, references `Grpc` and `ServiceDefaults`, a Helm `Job`). **Deliberately not part of the
sync service**: it is a one-off, it makes hundreds of outbound HTTP calls, and the sync engine's
plan/execute contract has nothing to say about bytes.

- **Scope: children who have actually attended a service.** People with at least one
  `DbAttendanceRecord` where `Deleted = false`. Most of the 1754-person roll is never seen at Impact
  Kids and pulling their photos is wasted storage and wasted requests.
- Skip anyone who already has a photo.
- Take only `d2dek0x2lg6bxh.cloudfront.net/…/members/…` URLs. The `cdn.elvanto.com.au` default and
  the gravatar fallbacks are placeholders, and storing them would be worse than storing nothing —
  they would read as "this person has a photo" and hide them from the Photos tool forever.
- Pre-filter out `_thumb_http` in the URL (the malformed ones) and treat a 403 as permanent: log it,
  do not retry, move on.
- Report at the end: considered / skipped-existing / skipped-placeholder / fetched / failed. Expect
  roughly 60% of the attempted fetches to succeed.

### Slice B5 — export for Elvanto staff

Since nothing can push a photo back through the API, the substitute is a bulk export the office
staff can drag into Elvanto's own UI.

- An admin page (under the existing Admin nav popout, beside Sync) that streams a zip.
- One file per person, named for the person: `firstname_lastname.jpg`, lower-cased, non-alphanumerics
  collapsed to `_`, with a numeric suffix on a collision (`asher_george_2.jpg`) so two people with
  the same name cannot silently overwrite each other in the zip.
- Extension follows the stored content type rather than being hard-coded.
- Streamed through the API like everything else — ~500 photos at ~50 KB is ~25 MB, which does not
  justify a presigned URL or a staging object.

## C. The Photos tool

A leader, on a phone, during a service, working through the children in front of them.

### Where it lives

Its own top-level nav entry beside Attendance — **not** under it, as agreed. It is scoped to
tonight's service the same way `Attendance/Tool` is (`ServiceId` query parameter, falling back to
today's service by date, then the most recent) so that logic is shared rather than re-derived.

### The list

Reuse `ListComponent` + `PersonDisplay`, exactly as `Attendance/Tool` does, so the cards look and
behave the same. A person appears when **all** of:

- they have an `AttendanceRecord` for this service with `LocalSignedOut == null` — the same test
  the "Signed In" filter uses, and it should call the same predicate rather than a second copy; and
- they have no photo, **or** `PhotoNeedsUpdate` is set.

The card shows the current photo where there is one, so "needs updating" is judged against the face
on screen. Empty state says the real thing — "Every child signed in has a current photo" — not a
blank panel.

### Capture

Tapping a name opens a full-screen capture view. In-app live camera, as agreed:

- `getUserMedia({ video: { facingMode: 'user' } })` into a `<video>`, a square crop guide, a large
  shutter, and a confirm/retake step. Nothing is written to the camera roll, and there is no app
  switch between children — the difference between three taps per child and a round trip through
  the camera app each time.
- A front/back camera toggle. Leaders will use both.
- Capture to a `<canvas>`, centre-crop to square, downscale to 500×500, encode JPEG at ~0.85. That
  matches Elvanto's own thumbnails and puts a photo at roughly 20–50 KB, so the whole roll is well
  under 100 MB.
- **Also a "Choose from library" file input**, for a leader who already took the photo outside the
  app. Same crop-and-downscale path, so both routes produce identical objects.
- If `getUserMedia` is unavailable or permission is denied, say so plainly and fall back to
  `<input type="file" accept="image/*" capture="user">`, which reaches the native camera and also
  does not touch the roll. Camera permission needs HTTPS — production has it, and the dev proxy on
  `https://localhost:7263` does too.
- Upload as `POST /api/people/{id}/photo`, leader-only. The response carries the new
  `PhotoVersion`, and the write raises the existing SSE data event so other clients refresh the
  person and pick up the new URL. It clears `PhotoNeedsUpdate`, which drops the child off the list —
  that disappearance is the tool's progress indicator.

### "Needs a new photo"

A checkbox on the person's Details card, in the same idiom as the existing "Family Guardian"
checkbox. `DbPerson.PhotoNeedsUpdate` (`bool`), additive migration, defaulting false. Set by anyone
who notices an out-of-date face; cleared automatically when a photo is taken.

### Media consent

**Not gated, and gated by config rather than by code.** An identification photo for signing a child
in is internal safeguarding, not publication, so the default is that everyone gets one. A
`Photos:BlockedMediaConsent` setting (empty by default) names the consent values that may not hold a
photo; setting it to `StrictlyNo` later suppresses capture, hides the control, and stops the
backfill for those people, with no code change. This is deliberately a knob, because it is a policy
question for the church rather than an engineering one.

## Suggested order

Backend before frontend within each, and the whole of A before B because A is small and proves the
descriptor path is still healthy before the larger change lands on top of it.

1. **A1** field end to end → **A2** sign-in error → **A3** sync descriptor
2. **B1** object store → **B2** serve endpoint → **B3** `PersonAvatar` (visible with nothing to show
   yet, which is a legitimate unfinished slice: it renders exactly as today)
3. **C** upload endpoint → capture view → photos list → **PhotoNeedsUpdate** checkbox
4. **B4** backfill worker → **B5** export

C before B4 on purpose. Three quarters of children need a photo taken whatever the backfill
achieves, so the tool is the thing that matters and the backfill is a nice head start that can land
a week later.

## Decided: the pickup wall never shows faces

The photo endpoint is **leader-only**. It gets no `AllowDisplay` marking and no `EnabledOrDisplay`
policy, so it falls through to `EnabledOnly` like everything else and an enrolled display cannot
reach it — see [Policies.cs](../../GSBC.ImpactKids.Grpc/Policies.cs) for why the fallback is what
makes that structural rather than a convention. A wall-mounted TV showing children's faces to a
foyer is a different thing from a leader's phone, and the answer is no.

## Before starting

### The chart is hand-written; Aspire's publisher is not in use

`Charts/impact-kids/` is the deployed chart and it is **hand-maintained** — 22 tracked files, still
being edited. `k8s-artifacts/` is the output of `publish.sh` (`aspire publish -o k8s-artifacts`) and
is **not tracked at all**: zero files under version control, last written 2026-02-09, referenced by
nothing but the script that produces it. The two have already diverged in shape — `k8s-artifacts`
still has a `webfrontend` component where the real chart has `wasm` and `yarp`.

So **SeaweedFS gets hand-written templates under `Charts/impact-kids/templates/s3/`**, in the idiom
of the existing `sql` and `rabbitmq` ones: a StatefulSet, a Service, a PVC, and a Secret carrying
the `-s3.config` identities. No publisher round trip, and nothing in `AppHost.cs` has to survive
code generation — it only needs to run the container for local dev.

Worth deleting `publish.sh` and `k8s-artifacts/` while in here, or at least saying in the README that
they are dead. A generator whose output nobody deploys is a trap for the next person who runs it and
believes the result.

### `getUserMedia` on iOS — build for it to fail, do not gate on it

**There is no pre-build iPhone test and nothing waits on one.** Asher will exercise the capture view
on a real phone once it is deployed to production. Do not halt, do not ask for a device, do not
build a throwaway test page.

The one historical trap — **iOS refusing `getUserMedia` outright in home-screen / standalone web
apps** — was fixed in iOS 14.3 (2020) and is long dead, so on any current iPhone this should simply
work. The live requirements are ordinary: serve over HTTPS (production and the dev proxy both do),
put `playsinline` on the `<video>` or iOS takes it fullscreen, and let the leader grant camera
permission once per origin, which persists.

Because the confirmation comes *after* deployment rather than before it, **the fallback is part of
the build, not a contingency.** Treat every one of these as a normal runtime state with a real
path through it, not an error to surface:

- `navigator.mediaDevices?.getUserMedia` missing entirely — an old or locked-down browser.
- The promise rejecting with `NotAllowedError` (permission refused) or `NotFoundError` (no camera).
- The stream opening but the `<video>` never reaching a non-zero `videoWidth` — the shape a
  standalone-mode failure would most likely take. Give it a timeout rather than a spinner forever.

Every one of those drops to the `<input type="file" accept="image/*" capture="user">` route, which
reaches the native camera, does not touch the camera roll, and works everywhere. Say so on screen in
one plain line — "Live camera unavailable, using your phone's camera app" — so a leader knows why
the flow changed rather than thinking the tool is broken. Do this from the first commit of the
capture view: if standalone mode does misbehave in production, the tool degrades instead of failing,
and the fix is a tweak rather than a rebuild.

### A branch

Per [AGENTS.md](../../AGENTS.md) the user creates branches and nothing is committed on `master`, so
nothing can land until there is one.

## Things to settle while building

- Photo history — currently a person has one photo and a re-shoot replaces it. Keeping the previous
  object is nearly free, given content-hash keys, and might be wanted for "that photo was better".
  Not built unless asked.
