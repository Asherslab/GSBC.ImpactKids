# Elvanto Bidirectional Sync

Bidirectional sync between the app and Elvanto, with a manual-review workflow for low-confidence person matches.

## Key entities

- `DbSyncPendingReview` — keyed on `(PersonId, ElvantoId)`. Persisted **outside** the main transaction (after `audit.FlushAsync`) so DryRun results survive the rollback.
- `DbSyncMetadata` — tracks `ManualReviewReason` and `LastSyncStatus` for existing checks.

## Review workflow

1. DryRun → low-confidence match → `DbSyncPendingReview` (`Status = Pending`) saved outside the transaction.
2. User opens the sync-operation detail page (`/Sync/{id}`) → the **Manual Review** tab shows items for this operation.
3. Approve / Deny buttons call `ISyncService.ApproveReview` / `DenyReview` (by review `Guid`).
4. Next wet run → engine checks the `pendingReviews` dictionary → **Approved** = link + field sync; **Denied** = skip both sides.

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

Follow the outside-transaction save pattern. An approved review remains a permanent record; once the wet run assigns the person's `ElvantoId`, the review becomes irrelevant to future syncs.
