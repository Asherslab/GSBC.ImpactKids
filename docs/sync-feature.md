# Elvanto Bidirectional Sync

Bidirectional sync between the app and Elvanto, with a manual-review workflow for low-confidence person matches.

## Key entities

- `DbSyncPendingReview` — keyed on `(PersonId, ElvantoId)`. Persisted **outside** the main transaction (after `audit.FlushAsync`) so DryRun results survive the rollback.
- `DbSyncMetadata` — the link between a person and an Elvanto record. Unique on `ElvantoId` **and**
  on `PersonId`, so `UpsertMetadata` asks both before adding a row; asking only `ElvantoId` added a
  second row for a person who had been compared to someone else's id and failed the whole run.
  `LastSyncStatus` is written but read by nothing — do not gate behaviour on it.

## Review workflow

1. DryRun → low-confidence match → `DbSyncPendingReview` (`Status = Pending`) saved outside the transaction.
2. User opens the sync-operation detail page (`/Sync/{id}`) → the **Manual Review** tab shows items for this operation.
3. Approve / Deny buttons call `ISyncService.ApproveReview` / `DenyReview` (by review `Guid`).
4. Next wet run → engine checks the `pendingReviews` dictionary → **Approved** = link + field sync;
   **Denied** = never link this pair, and (for a low-confidence match) the app person becomes
   eligible to be created in Elvanto as a separate person.

A review that is still `Pending` suppresses that person's outbound create, and says so with a
`ManualReviewQueued / CreateSuppressed:AwaitingReview` audit row. The suppression used to key off
`DbSyncMetadata.LastSyncStatus == ManualReview`, which nothing ever resets — so one trip through
the review queue suppressed a person's create permanently, with a bare `continue` and no row.

### Two kinds of review, asking opposite questions

`MatchStrategy` distinguishes them, and the same word means opposite things:

| | `LowConfidence:*` | `PotentialDuplicate:*` |
|---|---|---|
| Raised from | the matching loop, on a fuzzy candidate | the create path, when an unlinked person shares first+last name with someone already linked |
| **Approved** | **link** the two records, sync fields onward | **suppress the create** — they already exist in Elvanto |
| **Denied** | never link this pair, **and** create the app person separately | **create** them in Elvanto as a separate person |

Approving a duplicate deliberately does **not** link them: two app people cannot share one
`ElvantoId`, because `appByElvantoId` is built with `ToDictionary` on that key and would throw.
Merging the two app records is a separate, manual job — see the merge notes in
[the Full-run handover](./sync-full-run-handover.md).

The UI states the question on each card and labels the buttons accordingly ("Same person" /
"Different people" for duplicates), because "Approve"/"Deny" for both was genuinely ambiguous.

`PendingReviews` is mapped **one-to-many** on Person. It was one-to-one, which put a unique index
on `PersonId` alone and allowed a person only one review for all time — and since decided reviews
are never deleted, anyone judged a duplicate occupied that slot permanently, so a later
low-confidence review for them would fail the whole sync run.

## Writes to Elvanto are gated

`Elvanto:AllowWrites` (default `false` — no initializer, so absent config binds off) gates every
mutation. Request types declare `static abstract bool IsMutation`; `ElvantoService.SendMessage`
refuses a mutation above the line that touches `HttpClient` and logs the payload it would have
sent. Only `people/create.json` and `people/edit.json` mutate; the three read endpoints are
unaffected.

The gate is per-transport rather than per-caller on purpose: a new push added later cannot reach
the network without flipping the flag.

## The medical/allergy field

Elvanto gives one free-text custom field where the app holds structured allergy and medical rows,
so `MedicalAllergyFormat` defines a shape that survives a round trip:

```
Allergies: Peanuts (SEVERE) - carries EpiPen; Dairy
Medical: Asthma (SEVERE) - inhaler in bag; ADHD
```

One descriptor owns the field in both directions. Text that does not fit the grammar is never
discarded or guessed at — it returns as unrecognised and becomes an "Other" medical note holding
the raw words. Values that say nothing ("None", "nil", "no known allergies") are neither pushed
nor read back.

On a first sync the app wins, but not by deleting: Elvanto text the app does not already say is
carried across verbatim, so a free-text note listing more allergens than the app knows about
cannot be silently dropped.

Note that a field's direction comes from the seeded `SyncFieldConfigs` row, **not** from the
descriptor — `DefaultDirection` is only a fallback for fields with no row. Changing a direction
means editing the seed and adding a migration.

## The base value

**A field's two sides are compared against what they both held the last time they agreed, never
against a clock.** `ElvantoFieldSnapshots` holds both legs — `LastSeenHash`/`LastSeenValue` for
Elvanto and `AppHash`/`AppValue` for the app — and `FieldReconciler` is the three-way merge over
them. A **null `AppHash` means there is no base**, which is deliberate: the column was added without
a backfill, so the first run after it lands re-applies first-sync rules to everything and surfaces
every divergence that was invisible before.

`FieldChangeLogs` is no longer a gate. It supplies the app-side timestamp `ConflictResolver` needs,
and only when both sides have genuinely moved. A missing row means "app timestamp unknown", not "the
app did not change" — the second reading is what made a real, visible difference permanently
invisible on any database restored from a dump, because every edit predating the table has no row.

Two rules give the reconciler its shape:

- **Decide first, then let direction filter.** Direction used to be part of change detection, so a
  field the direction refused was never compared. Now the comparison is unconditional and a
  direction refuses an *outcome*, which can be named in an audit row.
- **A base may advance only when the field has no outstanding app-side change, or when the request
  that was actually sent carried that field and landed.** With writes off nothing lands, so every
  outbound change stays outstanding and is offered again next run.

`IFieldSyncDescriptor.ApplyToElvantoRequest` returns `bool` for that last rule: whether the payload
genuinely carries the field. A descriptor that declines — `SchoolGradeId`, `FamilyGuardian`, a
media-consent value that is not one of the four options — must say `false`, because a base advanced
on a field that was never sent buries the change it was given.

### Clearing a field

Verified against the live API on 2026-08-26: Elvanto **ignores an explicit `null` and an omitted
field alike**, answers `ok`, and moves `date_modified` without changing the value. Only an **empty
string** clears — on `email`, `mobile`, the standard `birthday` date, and the `select`, `datepicker`
and `textarea` custom fields.

So a clear has to be sent deliberately, as `""`, and `[JsonIgnore(WhenWritingNull)]` is not the
cause of anything. Descriptors decline a null and send an empty string; the base is what tells them
apart, because `A ≢ B.app` with `A` null is a deliberate clear while `A ≡ B.app` with both null is
nothing to say.

## Divergence is recorded, never implied

`SyncEventType.Diverged` says "these two differ and the engine chose not to act", with both values
and a reason on the row. Nine paths through the field decision used to end in no action, no audit
row and no counter, which made a real divergence indistinguishable from nothing to do. The operation
page gives them their own tab and stat tile, because they are a work-list rather than a footnote.

## Never trust a partial Elvanto fetch

`RetrieveElvantoPeople` returns the whole roll or throws `ElvantoFetchException`; there is no
partial-success outcome, and it holds Elvanto to its own reported total. A full-scope sync also
aborts before the archive step if the roll covers under 90% of the linked people.

Both guards exist because absence from the fetched list is treated as proof of deletion: a single
dropped page once archived 726 children, and six of the seven tables referencing a person are
`ON DELETE CASCADE`.

## gRPC methods on `ISyncService`

- `ReadPendingReviews()` — streams all `SyncManualReviewEntry`.
- `ApproveReview(ManualReviewActionRequest)` — sets `Status = Approved`.
- `DenyReview(ManualReviewActionRequest)` — sets `Status = Denied`.

## DryRun persistence pattern

`SyncAuditLogger.FlushAsync` calls `db.ChangeTracker.Clear()` then saves audit logs outside the transaction. Pending reviews follow the same pattern — saved after `FlushAsync` via `SaveNewPendingReviewsAsync`. This is what lets a DryRun leave behind reviewable state even though its transaction is rolled back.

## UI

- `Individual.razor` — "Manual Review" tab with approve/deny cards per item; shown only when the operation has `ManualReviewQueued` events. It mirrors the pending-review store into a local field, so it must seed that field explicitly after `RefreshAll()` — see [Front-end Store Architecture](./frontend-store-architecture.md).
- `Multiple.razor` — warning banner showing the pending-review count after each sync.

## Why

A user needs to approve low-confidence matches from a DryRun before running the wet run.

## Extending the review UX

Follow the outside-transaction save pattern.

An approved review remains a permanent record. For a low-confidence match it does become
irrelevant once the wet run assigns the person's `ElvantoId`. For a **potential duplicate** it
does not: the row stays meaningful indefinitely, because it is the durable statement that two app
records are the same person, and the future merge feature reads exactly those rows to find its
work. Do not add cleanup that deletes decided reviews.
