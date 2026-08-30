---
title: Person photos
kind: reference
status: current
module: people
verified: 2026-08-30
code:
  - GSBC.ImpactKids.Grpc/Features/People/Photos
  - GSBC.ImpactKids.WASM/Features/People/Components/Individual/PersonAvatar.razor
  - GSBC.ImpactKids.WASM/Features/People/Components/Individual/PhotoCapture.razor
  - GSBC.ImpactKids.WASM/Features/People/Pages/Photos.razor
  - GSBC.ImpactKids.WASM/wwwroot/js/photoCapture.js
  - GSBC.ImpactKids.Workers.PhotoBackfill
---

# Person photos

How a child's face gets into the app, out of it, and onto a leader's screen. Read this before
touching the photo endpoints, the capture view or the object store client. Where the photos
physically live is [the object store](../infrastructure/object-store.md).

## The shape of it

A photo is **an object in the store keyed by the hash of its own bytes**, and the person row carries
only that hash in `DbPerson.PhotoVersion`. Nothing is ever overwritten: a re-shoot writes a new
object and points the row at it.

That one decision buys most of the design:

- The version is in the serve URL, so a re-shot photo is a *different* URL. The response can be
  `Cache-Control: private, max-age=31536000, immutable` with no revalidation, no ETag round trip and
  no stale face.
- `rclone copy --size-only --immutable` is a correct backup, because a name's content never changes.
- Two people who somehow produce identical bytes share one object rather than two.

`Person.PhotoVersion` on the contract is **a token, never the bytes.** 1772 people is far too much
image data for app state; a face is an ordinary `<img>` on the same origin, so the cookie the browser
already holds is attached automatically and there is no JS, no token plumbing and no fetch wrapper.

## Endpoints

| Route | Who | Notes |
|---|---|---|
| `GET /api/people/{id}/photo?v={hash}` | leader | 404 when there is no photo, or when `v` is not the current version |
| `POST /api/people/{id}/photo` | leader | body is the image itself, not a multipart form |
| `GET /api/people/photos/export` | leader | streamed zip, one file per person |

**Leader-only structurally, not by convention.** None of these carries an `AllowDisplay` marking or
the `EnabledOrDisplay` policy, so they fall through to the service's `EnabledOnly` fallback and an
enrolled wall display is never authenticated against them at all. See [Policies.cs](../../../GSBC.ImpactKids.Grpc/Policies.cs).
**The pickup wall never shows faces** — a wall-mounted TV showing children's faces to a foyer is a
different thing from a leader's phone, and the answer is no.

`Cache-Control` is set on the success path only. Every 404 is a state that changes — a child with no
photo yet will have one tonight — so caching one immutably for a year would hide the new photo behind
the old absence.

The endpoints are mapped only when a `Photos` section supplies a service URL **and both
credentials**. Checking the URL alone would not do in the cluster: it comes from the ConfigMap and is
therefore always set, while the keys come from `photos-secret` — so a missing Secret would register a
store with blank credentials and 403 on every operation, instead of the feature simply being off.

**Photos must never be able to stop children being signed in**, and two things enforce that:

- The startup `EnsureBucketAsync` is best effort. There is no ordering guarantee in the cluster, so
  the object store may not be up when this service starts; an unreachable store logs an error and
  the service carries on. `PutAsync` creates the bucket on demand if that startup call did not.
- `photos-secret` is mounted `optional: true` on the gRPC deployment. Without that, a Secret nobody
  has created yet stops the pod starting at all.

Verified by pointing the service at a dead port: the app came up, `/bff/user` answered 200, and the
photo endpoint answered 500 while every avatar fell back to its initial. **500 rather than 404 is
deliberate** — 404 means "this person has no photo", and a broken store must not be able to hide
behind it.

## Two traps in the S3 client, both of which failed silently

Both are in `PhotoStore.PutAsync`, and both are load-bearing:

- **`DisablePayloadSigning` cannot be used.** The AWS SDK refuses it over plain HTTP, and the store
  is reached over HTTP inside the cluster. Setting it turns every upload into a 500.
- **`UseChunkEncoding = false` is required.** With signing on, the SDK defaults to `aws-chunked`
  streaming, and SeaweedFS 3.98 stores that framing verbatim instead of decoding it. The object is
  then the right size, has the right content type and a valid database row, and is *not* an
  image — it begins `<hex length>;chunk-signature=…` rather than `FF D8 FF`. Nothing fails; the face
  just never renders.

Because that second one was invisible to every other signal, **`PhotoStore.GetAsync` checks magic
bytes on every read** and refuses anything that is not a JPEG, PNG or WebP — logging loudly and
answering 404, which the avatar already handles. Keep that check.

## `PersonAvatar` — the initial is the component, the photo is painted over it

The coloured initial always renders. It is the ground state, not a fallback branch. The `<img>` is
emitted only when `PhotoVersion` is non-null, absolutely positioned over the initial so it occupies
no space either way, and `onerror` hides it to reveal the initial underneath. A 404, a dropped
connection, an offline phone and an object the store has lost all land on that one path — no
broken-image glyph, no layout shift.

**The fade-in is a CSS animation, not an `@onload` handler.** Responses are cached immutably for a
year, so on any repeat view the image is already complete before Blazor attaches the handler; the
event never fires, and anything waiting on it leaves the photo at opacity 0 with the initial showing
through.

Two scoped-CSS details: the `MudAvatar` needs `::deep` from the plain wrapper, but the `<img>` is in
the component's own markup and is stamped directly, so a `::deep` there matches nothing. And the
rounding lives on the clipping wrapper, because the image's parent is that wrapper rather than the
MudAvatar beside it — `border-radius: inherit` would inherit 0 and square off a rounded avatar.

## The Photos tool

Its own top-level nav entry beside Attendance. A person appears when they are signed in for tonight
**and** either have no photo or carry `PhotoNeedsUpdate`. Taking the photo clears the flag, which
drops them off the list — that disappearance is the tool's only progress indicator.

"Signed in" is `AttendanceRecord.IsSignedIn`, shared with the Attendance tool's filter rather than
copied: two screens that disagree about who is present is worse than either screen being wrong.

### Capture, and why the fallback is not a contingency

The live camera is the fast path — `getUserMedia` into a `<video>`, a square crop guide, a large
shutter, confirm/retake, and a front/back toggle. Nothing touches the camera roll and there is no app
switch between children.

**Every way it can fail is an ordinary runtime state with a real path through it**, because the
iPhone confirmation happens after deployment rather than before:

- `navigator.mediaDevices?.getUserMedia` missing — an old or locked-down browser, or a non-secure origin.
- `NotAllowedError` (permission refused) or `NotFoundError` (no camera).
- The stream opening but `videoWidth` never becoming non-zero. **Bounded at five seconds**, because
  an endless spinner is the failure mode to avoid.

All of them drop to `<input type="file" accept="image/*" capture="user">` with one plain line on
screen. The library route is always offered, not only on failure — a leader who already took the
photo outside the app needs it either way. Both routes go through the same crop-and-downscale, so
they produce byte-identical objects for the same image, and therefore one object rather than two.

A pending permission prompt is *not* a failure and is not timed out: the view waits, and the wording
stays neutral until the camera has actually finished trying.

**The preview is mirrored; the stored photo is not.** The mirror is CSS only, so the frames the
`<video>` holds are already unmirrored and the canvas draws them straight. Do not "fix" this by
flipping on the canvas — that re-mirrors the face, and it hides itself, because a mirrored preview
and a mirrored capture agree with each other.

Photos are 500×500 JPEG at q0.85, which is what Elvanto's own thumbnails are and puts a photo at
roughly 20–50 KB. Measured across a real backfill: 21 KB average.

## Media consent is a knob, not code

`Photos:BlockedMediaConsent` names the consent values that may not hold a photo. **Empty by default,
and that is the decision**: an identification photo for signing a child in is internal safeguarding,
not publication, so everyone gets one. Setting it later suppresses capture and stops the backfill
with no code change, because it is a policy question for the church rather than an engineering one.
The check lives on `PhotoStoreConfig` so the endpoint and the backfill cannot disagree.

## The Elvanto backfill

`GSBC.ImpactKids.Workers.PhotoBackfill` is a one-off pull of what Elvanto already holds. Deliberately
not part of the sync service: it runs once, it makes hundreds of outbound calls for bytes, and the
sync engine's plan/execute contract has nothing to say about bytes.

- Scoped to people with a non-deleted `AttendanceRecord`. Most of the roll is never seen at Impact
  Kids.
- Skips anyone who already has a photo.
- Takes only `d2dek0x2lg6bxh.cloudfront.net/…/members/…` URLs. The `cdn.elvanto.com.au` default and
  the gravatar fallbacks are placeholders, and storing one would read as "has a photo" and hide the
  child from the Photos tool forever.
- Pre-filters `_thumb_http` and treats a failure as final — see
  [the Elvanto API reference](../elvanto/api-reference.md) for why those 403s are permanent.
- Read-only, and it forces the Elvanto write gates off regardless of configuration.

Off by default in the chart and not a Helm hook: it is a head start, not a correctness requirement.

**Most children will need a photo taken in the app whatever the backfill achieves** — of ~195
children per 1000 people, only 79 have any Elvanto photo and roughly 40% of those are dead links.

## Export, because Elvanto cannot accept a photo back

There is **no way to write a picture through the Elvanto API** — neither `people/edit` nor
`people/create` accepts a `picture` parameter, at the top level or under `fields`. So the app owns
photos from the backfill onwards, and office staff get a zip to drag into Elvanto's own admin.

One file per person, `firstname_lastname.jpg`, lower-cased with non-alphanumerics collapsed to `_`
and a numeric suffix on a collision — two people really are called the same thing, and a zip that
keeps only the last of them loses a photo nobody notices until the child has none in Elvanto. The
extension follows the stored content type.

**`ZipArchive` needs `AllowSynchronousIO` for that one request.** It writes the central directory
synchronously on dispose, Kestrel disallows synchronous response writes, and the failure is nasty: a
200 carrying a truncated archive with no central directory, because the headers are long gone by the
time the dispose throws. There is no async `ZipArchive` to use instead.

## Photo history

A person has one photo and a re-shoot replaces the row's pointer. The previous object is *not*
deleted, so a history exists in the bucket for free — but nothing reads it, and nothing prunes it.
See the object store doc for why that accumulation is deliberate and what it costs.
