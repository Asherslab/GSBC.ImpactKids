# Elvanto Bidirectional Sync

Bidirectional sync between the app and Elvanto, with a manual-review workflow for low-confidence person matches.

## Key entities

- `DbSyncPendingReview` — keyed on `(PersonId, ElvantoId)`. Written during Decide, so a review is
  waiting before anything has been applied.
- `DbSyncPlannedChange` — one row per decision, and the thing a person reads before pressing Execute.
- **There is no `DbSyncMetadata` and no `DbSyncFieldConfig`.** The link between a person and an
  Elvanto record is `DbPerson.ElvantoId`; how they came to be linked is in `SyncAuditLogs` and the
  plan. A field's direction and tie-breaking are on its descriptor. Both tables were write-only
  duplicates of facts held elsewhere, and each cost a whole-run failure or a silently dropped change
  while it existed.

## Review workflow

1. Decide → low-confidence match → `DbSyncPendingReview` (`Status = Pending`).
2. User opens the sync-operation detail page (`/Sync/{id}`) → the **Manual Review** tab shows items for this operation.
3. Approve / Deny buttons call `ISyncService.ApproveReview` / `DenyReview` (by review `Guid`).
4. Next run → engine checks the `pendingReviews` dictionary → **Approved** = link + field sync;
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

A field's direction and its tie-breaking side come from **the descriptor**, and nowhere else.
They used to live in a seeded `SyncFieldConfigs` row that overrode the descriptor entirely, with
eleven rows that all matched their descriptor exactly and no reader outside the engine — no settings
UI, no gRPC method, no admin page. Its only behavioural contribution was to put the answer in two
places, and two corrective migrations were needed to get them back in step, one of which cost a
family move dropped with no audit row.

## Every run is Decide, then Apply

| Phase | Reads | Writes | Sends |
|---|---|---|---|
| **Decide** | Elvanto + app | the plan, the divergences, the pending reviews, and the bases of fields that already agree | nothing |
| **Apply** | the plan + live state | app people, bases, the audit trail | Elvanto |

**There is no mode.** `CreateSync` decides and stops; `ExecutePlan` applies a plan someone has read.
Two calls, always in that order, and nothing that does both at once.

There used to be three modes — `Full`, `AppOnly`, `DryRun` — which were three answers to "how much of
Apply should I skip?". The honest form of that question is "has anyone looked at the plan yet?", and a
separate Execute answers it by existing. `SyncWithElvantoRequest` is now an empty marker: an RPC needs
a request type, but there is nothing left to put on it.

**AppOnly is not lost.** An Execute with `Elvanto:AllowWrites=false` applies the inbound half and
records every outbound as suppressed with the switch that stopped it — which is what AppOnly did,
decided by configuration rather than by whoever picked from a dropdown.

Decide and Apply were once *different* create paths, so a preview was a plan preview rather than a
rehearsal — it structurally could not exercise `SaveChanges`, the change interceptor, the payload
builder, the `"new"`-family chain or the failure branch. There is now one path to walk.

**Decide touches nothing in `People`.** It is genuinely read-only on the app side, so the audit trail
cannot record `Created`, `Match`, `FieldUpdated` or `Archived` in the past tense for a run that did
none of them. Decide writes only `Diverged` and `ManualReviewQueued` rows; the plan carries everything
else, and Apply writes the past-tense rows when it acts.

An agreement settles its base during Decide rather than waiting for Apply, because there is nothing
to apply: recording that two sides already say the same thing changes neither of them.

### Only one Apply at a time

`ApplyPlanAsync` takes a Postgres advisory lock before it reads anything, and refuses immediately
rather than queueing if it cannot have it. A second Execute is told *"another sync execution is
already running… the plan is untouched"* and nothing is read or written.

The per-item staleness check cannot cover this, and it is worth being clear why. Every guard in Apply
re-reads the two **sides** — but two executions in flight have both read `Status == Pending` before
either has written a status back, and neither side has moved while the other is still mid-flight, so
both pass every check and both do the work. The visible failure is two people created in Elvanto for
one plan row, which no later run can undo. Sequential re-execution was always safe; simultaneous was
not.

It is an advisory lock rather than an `Applying` status column or a `SemaphoreSlim` for two reasons.
The lock is held by the **connection**, so a process that dies mid-apply releases it, instead of
leaving a row saying `Applying` forever with nobody to clear it. And it is held by the **database**,
so it still holds if the gRPC service is ever run as more than one replica — which a static semaphore
quietly would not.

### `DbSyncPlannedChange`

One row per decision, with both observed hashes on it — the same base primitive at a different
moment. **Apply re-reads both sides and compares against them.** An item whose reading has moved is
marked `Stale`, skipped and reported; nothing is clobbered on the strength of a stale observation.

**Apply executes only what the plan contains and never discovers new work.** Anything that appeared
since Decide belongs to the next plan, and saying so plainly is what makes the button safe to press.

`DbSyncOperation.PlanExpiresAt` defaults to four hours (`Elvanto:PlanExpiryHours`). Past it Apply
refuses the whole plan rather than any part of it. The per-item check is the real protection; expiry
guards against a failure it cannot catch — a stale *item* is one whose values moved, while expiry
guards against the *set* of items being wrong, because people created, deleted or merged in Elvanto
since Decide ran are not in the plan for any per-item check to look at.

`DbSyncPendingReview.SyncOperationId` is a real foreign key. A review used to be found by joining
through the operation's audit rows, so one failed flush made it unreachable from the page meant to
action it — and that flush shared a `try` with the review save, logged as "audit logs", so a failure
discarded every review from the run while the method still returned `Success`.

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
genuinely carries the field. A descriptor that declines — a demotion from `FamilyGuardian`, a school
grade the app cannot name in Elvanto's terms, a media-consent value that is not one of the four
options — must say `false`, because a base advanced on a field that was never sent buries the change
it was given.

### Clearing a field

Verified against the live API on 2026-08-26: Elvanto **ignores an explicit `null` and an omitted
field alike**, answers `ok`, and moves `date_modified` without changing the value. Only an **empty
string** clears — on `email`, `mobile`, the standard `birthday` date, and the `select`, `datepicker`
and `textarea` custom fields.

So a clear has to be sent deliberately, as `""`, and `[JsonIgnore(WhenWritingNull)]` is not the
cause of anything. Descriptors decline a null and send an empty string; the base is what tells them
apart, because `A ≢ B.app` with `A` null is a deliberate clear while `A ≡ B.app` with both null is
nothing to say.

### An unknown side is not a value

**"Elvanto holds nothing" and "I cannot read what Elvanto holds" are different answers.** Collapsing
them into one null is a data-loss bug rather than a tidiness one, and `TranslateElvantoValue` now
reports which it is. A value it could not read is `Diverged: ElvantoValueUnknown`, and neither side
is touched.

Two things are unreadable:

- **A blank `family_id`.** Elvanto has no household for that person; that is not evidence they have
  none. Read as a value it drove an inbound write of null — which the descriptor turned into a fresh
  Guid and a brand-new one-person household. On real data it proposed to move 397 people out of the
  app's "no family yet" bucket in a single run.
- **An Elvanto household with no other member this app knows**, and **an Elvanto school grade with no
  `DbSchoolGrade` row**. Neither can be turned into an app value without inventing one. The grade
  case used to clear the child's grade, with an audit row that read as a legitimate clear.

**Known-empty and unknown are different answers.** A known-empty app value is still a deliberate
clear and is still pushed, as `""`.

## Divergence is recorded, never implied

`SyncEventType.Diverged` says "these two differ and the engine chose not to act", with both values
and a reason on the row. Nine paths through the field decision used to end in no action, no audit
row and no counter, which made a real divergence indistinguishable from nothing to do. The operation
page gives them their own tab and stat tile, because they are a work-list rather than a footnote.

## Family, and what the sync will not guess at

**Every run covers the whole roll.** `Scope` (`All` / `Person` / `Family`) is gone, along with
`PersonId` and `FamilyId` on the request and on `DbSyncOperation`.

Scoping was never sound and was removed rather than repaired. A scoped fetch was not a scoped roll:
asking Elvanto about one person or one family pulled the *whole* roll the moment any member was
unlinked, so the matcher could find them — while the app side loaded only that person or family.
Nothing then separated "the person this run is about" from the other seventeen hundred, and every one
of them read as somebody to create. `Scope=Person` on an unlinked person planned ~1718 spurious local
creates.

Two guards existed only to contain that, and both are gone with it: `SyncWorkingSet.MayCreateLocalPeople`,
and a scope check at the top of `DecideArchives`. To narrow a run's *effect*, use the allow lists
(`Elvanto:AllowedUpdatePersonIds`, `Elvanto:AllowedCreatePersonIds`) — those gate the write, which is
the thing worth gating.

**Family is compared in the app's terms** — "is this person in the right local family?", which is a
fact the app owns. `person.FamilyId` on one side, and on the other the local family Elvanto's
household corresponds to.

It was compared the other way round, in Elvanto's terms, because the household-to-family map was
self-confirming: it was seeded from every linked person, so a person alone in their Elvanto
household mapped it straight back to the local family they were already in. `TranslateElvantoValue`
now **excludes the person being asked about**, exactly as `ResolveFamilyInElvanto` always has, which
closes that at the source — and it ranks candidate families by how many members they have in the
household, because one household can span two local families and picking whichever came first moved
people into a relative's family at random.

Comparing in Elvanto's terms had its own failure once the map was fixed: it asked "which household
does this person's local family *mostly* correspond to?", so a family split across two households
disagreed with itself forever. The inbound move was a no-op that settled the base anyway, and the
next run read the app's own grouping as a fresh change and planned to push the person back.

Outbound still speaks Elvanto's language: the household this person's local family sits in, or
`"new"` when it has none — **which is a household to create, not a family to clear**.

`FamilyIdDescriptor.SetOnApp` only ever assigns a family it was given. It used to fall back to
`Guid.NewGuid()`, so an unreadable value moved the person into a brand-new one-person household —
and since the field then had no Elvanto value to record, it recurred on every run.

The same distinction applies to school grade: an Elvanto grade with no `DbSchoolGrade` row is
unreadable, not absent, and clearing a child's grade because of it produced an audit row that read
as a legitimate clear.

### School grade goes both ways

`SchoolGradeId` was `InboundOnly` for two reasons and is now `Bidirectional`. The payload had no
`school_grade` at all, so the direction was the only thing stopping a "would push" row for a change
the request body could never carry; it carries the field now. And Elvanto rolls every child's grade
over yearly, which the old engine could not tell apart from an app-side edit — it compared an edit
timestamp against a poll timestamp. The three-way merge can: a rollover moves Elvanto's leg alone and
is `ElvantoChangedAlone`, applied inbound with no clock consulted at all.

It is translated the same way family is: compared in the app's terms, pushed in Elvanto's, with
`BuildComparison` resolving the local Guid back to `DbSchoolGrade.ElvantoId`. All fifteen local
`ElvantoId` values match the ids the live API returns, one for one.

Three things about the wire format, all verified against the live API on 2026-08-27, and each one
wrong in the first cut of this change:

- **`school_grade` is a standard optional people field, so it travels under `fields`**, beside
  `birthday`. At the top level it is rejected outright: `A param does not exist (school_grade)`.
- **The value is the grade id, not its name**, despite the docs describing it as "the name of the
  school grade". The name works only where the name is not numeric — this account's grades are named
  `1`–`12` plus Prep, Kindergarten and Nursery/Pre-school, and `"7"` answers with a 500 while that
  grade's id succeeds. Twelve of the fifteen would have been unpushable.
- **A school grade cannot be cleared through the API at all.** A null answers `ok` and changes
  nothing; `""` and `"0"` both answer 500; every spelling of "none", including `-- None --`, is
  rejected as an invalid value. Only Elvanto's own UI can empty the field.

That last point makes the descriptor's refusal the only behaviour available rather than a
preference. **A grade the app cannot name in Elvanto's terms is never sent.** A child with no grade
and a local grade row with no `ElvantoId` both arrive as null and are declined, reported as
`NotCarried:`. Note that the general "an empty string is a deliberate clear" rule above does **not**
hold for this field.

## Never trust a partial Elvanto fetch

`RetrieveElvantoPeople` returns the whole roll or throws `ElvantoFetchException`; there is no
partial-success outcome, and it holds Elvanto to its own reported total. A run also aborts before the
archive step if the roll covers under 90% of the linked people, or if Elvanto returns nothing at all.

Both floors used to be qualified by "if this is a full-scope run". Every run is now the whole roll, so
they apply unconditionally — which is what they were always for. With scope gone, this coverage floor
is the *only* thing standing between a short Elvanto read and a mass archive.

Both guards exist because absence from the fetched list is treated as proof of deletion: a single
dropped page once archived 726 children, and six of the seven tables referencing a person are
`ON DELETE CASCADE`.

## Known gaps

Two things that are understood, deliberately not fixed, and should be read before anyone concludes
the engine is airtight. Both are narrow; neither is a reason to keep writes off.

### The create staleness check is skipped when the household was minted mid-apply

`ElvantoPersonSyncService.Apply.cs`, in `ApplyCreatesInElvantoAsync`:

```csharp
if (SyncHash.Of(payload) != item.ObservedAppHash && elvantoFamilyId == item.ProposedValue)
```

The family id is part of the create payload, so a person whose household was minted by an *earlier
item in the same apply* hashes differently for a reason that is not a change to the person — hence
the second clause. But `&&` means the whole check is skipped in exactly that case, so the second and
later members of every new household are created from a payload nobody re-verified. Edit that child
between Decide and Execute and the edit goes to Elvanto unannounced instead of being marked `Stale`.

The fix is to compare like with like rather than to skip: rebuild the payload with `item.ProposedValue`
as the family, hash that, and compare unconditionally. Left alone because it wants its own test over
the sibling-create path, and the window is one plan's lifetime (`Elvanto:PlanExpiryHours`, 4 by
default).

### The `FamilyId` base is settled in the wrong terms after an outbound push

Family is compared in the **app's** terms but pushed in **Elvanto's**, and it is the only field where
those differ — `BuildComparison` sets `OutboundValue` to the Elvanto household id (or `"new"`) for
`FamilyId` and to the app-side value for everything else.

`ApplyOutboundFieldsAsync` settles the base's Elvanto leg from `item.ProposedValue`, which for family
is therefore a household id or the literal string `"new"`, while every comparison reads that leg as a
local family Guid. So after any outbound family push, `BaseElvantoHash` is in a space nothing else
uses.

It does not loop, because `FieldReconciler` returns `Agreed` on hash equality *before* it consults the
base — and once Elvanto has taken the push, the two sides agree. It bites only if a later change makes
the two sides differ again: `elvMoved` is then computed against `"new"`, is meaninglessly true, and a
clean "the app changed alone" is decided as a two-sided conflict instead. The fix is to settle that
leg from the rebuilt comparison's `ElvantoValue`, as the inbound path already does, rather than from
the proposed value.

**This is not the old ping-pong**, which was a different and much larger problem — a household map
derived from the roll on every run, so a move changed the evidence the map was built from. That is
fixed: `ElvantoFamilyLinks` persists the pairing. See
[the refactor log](./work/2026-08-elvanto-sync-refactor.md).

## gRPC methods on `ISyncService`

- `ReadPendingReviews()` — streams all `SyncManualReviewEntry`.
- `ApproveReview(ManualReviewActionRequest)` — sets `Status = Approved`.
- `DenyReview(ManualReviewActionRequest)` — sets `Status = Denied`.

## Audit and review persistence

`SyncAuditLogger.FlushAsync` saves audit rows on its own; pending reviews follow via
`SaveNewPendingReviewsAsync`. Both are written during Decide, so a divergence and a review are
readable before anything has been applied.

This was once described as surviving a rollback. **There is no transaction in the sync engine at
all** — there is nothing for Decide to roll back, since it writes no `People` rows, and Apply
deliberately does not hold one open across Elvanto HTTP calls: doing that meant every rollback undid
half the world. Local state is committed before the sends, and the results are reconciled in a short
save afterwards.

## UI

- `Individual.razor` — "Manual Review" tab with approve/deny cards per item; shown only when the operation has `ManualReviewQueued` events. It mirrors the pending-review store into a local field, so it must seed that field explicitly after `RefreshAll()` — see [Front-end Store Architecture](./frontend-store-architecture.md).
- `Multiple.razor` — a single **Decide Plan** button, the run list, and a warning banner showing the
  pending-review count. It no longer loads the people store: that existed only to fill the Person and
  Family scope dropdowns, so the run list no longer waits on ~1700 people to render.
- **Execute confirms first.** The button on the run list opens a message box naming the pending item
  count and saying the changes go to this app and, where writes are enabled, to Elvanto. The engine's
  guards are all per-item, and none of them notices that the person pressing Execute meant to press
  View — so the count has to be shown before the call, not reported after it.

## Why

A user needs to approve low-confidence matches from a decided plan before executing it.

## Extending the review UX

Follow the outside-transaction save pattern.

An approved review remains a permanent record. For a low-confidence match it does become
irrelevant once an Execute assigns the person's `ElvantoId`. For a **potential duplicate** it
does not: the row stays meaningful indefinitely, because it is the durable statement that two app
records are the same person, and the future merge feature reads exactly those rows to find its
work. Do not add cleanup that deletes decided reviews.
