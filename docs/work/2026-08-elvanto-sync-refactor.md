---
title: Elvanto sync — base values, plan-then-execute, and splitting the orchestrator
kind: plan
status: in-progress
opened: 2026-08-25
verified: 2026-08-26
code:
  - GSBC.ImpactKids.Grpc/Features/People/Sync
  - GSBC.ImpactKids.Grpc/Features/Sync
  - GSBC.ImpactKids.Grpc/Data/Models/Sync
  - GSBC.ImpactKids.WASM/Features/Sync
---

# Elvanto sync — base values, plan-then-execute, and splitting the orchestrator

Three changes that turn out to be one change. The evidence they answer is in
[2026-08-elvanto-sync-review.md](2026-08-elvanto-sync-review.md); findings are cited as **F1**–**F15**.

**No writes to Elvanto are enabled by any of this.** `AllowWrites`, `AllowCreates` and
`AllowUpdates` stay `false` and `MaxWrites` stays `0` throughout. Nothing here touches the write
guards.

## The one idea

The engine currently answers *"did the app change?"* by comparing an edit timestamp against a poll
timestamp. Those are two independent clocks, and every way they drift buries a real change (**F1**,
**F2**). The fix is to stop asking a clock and start asking a **base value**: what both sides held
the last time they agreed.

That single change cascades:

- **A three-way merge** becomes possible, and the change log demotes to what it should always have
  been — a tiebreak timestamp on genuine two-sided conflicts.
- **The base can only advance on evidence**, which deletes the hold, `appWonConflictFields` and
  `pushLanded`-as-correctness.
- **Deciding separates from applying**, because the base is written at the point a decision is
  *settled*, not at the end of a run. That separation is exactly what part C needs.
- **The split falls out**: Decide and Apply are the two collaborators the 1270-line file has been
  missing.

Do not read this as "the snapshot table was a mistake". It is doing a job nothing else can do —
per-field change detection on the Elvanto side, which `date_modified` structurally cannot provide
because it is per person (`ServiceModels.cs:140`, and the code says so). **Removing the base and
resolving everything on timestamps would be a regression, not a simplification**: one Elvanto edit
to any field makes `date_modified` newer than *every* pending app change on that person, so they
would all lose to stale Elvanto values. The base is half-built, not wrong. Finish it.

---

# Part 1 — The base value

## The model

Replace `DbElvantoFieldSnapshot`'s Elvanto-only memo with both sides at last agreement:

```
DbSyncFieldBase   (EntityType, EntityId, FieldName)   -- unique, as today
    ElvantoHash, ElvantoValue     -- today's LastSeenHash / LastSeenValue
    AppHash, AppValue             -- new
    AgreedAt                      -- today's LastSeenAt, but now means "agreed", not "polled"
```

Two columns. `AppHash`/`AppValue` are what make *"has the app moved?"* answerable without consulting
a clock, and holding **both** sides covers the two fields compared in the other side's terms —
`FamilyId` in Elvanto's (`:837-842`) and `SchoolGradeId` in the app's (`:827-828`).

## The decision

With `A` = current app value, `E` = current Elvanto value, `B` = the base:

```
Direction == Disabled          -> skip, and DO NOT touch the base
A ≡ E                          -> agree; write base; done
no base row                    -> first sync: FirstSyncPrecedence (+ MergeForFirstSync)
A ≡ B.app,  E ≢ B.elv          -> Elvanto changed alone   -> inbound
A ≢ B.app,  E ≡ B.elv          -> app changed alone       -> outbound
A ≢ B.app,  E ≢ B.elv          -> conflict -> ConflictResolver on date_modified vs FieldChangeLogs
                                            -> then PrecedenceOnTie
anything refused by Direction or IsValidInboundValue
                               -> audit Diverged, leave the base alone
```

`DbFieldChangeLog` and `FieldChangeTrackingInterceptor` are **unchanged**. The log stops being a
gate and becomes the `appChangedAt` argument to `ConflictResolver`, consulted only on the last line.
A missing row there now means "app timestamp unknown" and falls through to `PrecedenceOnTie`,
instead of today's "therefore the app did not change".

Timestamps are consulted only when both sides genuinely moved — which is where `date_modified` being
a per-person upper bound is acceptable, and where the existing code already uses it correctly.

## The rule that closes the bug class

> A field's base may advance only when that field has no outstanding app-side change, **or** the
> request that was actually sent carried that field.

"Carried that field" means *inspect the built payload after `ApplyOutbound` ran*, not "the descriptor
was asked" and not "the call returned ok". This needs one interface change:

```csharp
// IFieldSyncDescriptor
bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value);   // was void
```

returning whether it actually set anything. `MediaConsentDescriptor.cs:31` and the two empty
`InboundOnly` stubs then report `false` honestly instead of silently no-opping (**F8**).

### Clearing a field — answered against the live API, 2026-08-26

Tested directly against Elvanto on the linked test person `a8074b15-1175-4ee3-85c1-2c4b9683e857`
("Permission Testing User"), by `curl` outside the app. Every value was restored afterwards.

| Sent | Result |
|---|---|
| `"field": ""` | **clears the value** — confirmed on `email`, `mobile`, the standard `birthday` date, the `select` custom field (media consent), the `datepicker` custom field (first time) and the `textarea` custom field (medical notes) |
| `"field": null` | **silently ignored.** Elvanto answers `ok`, `date_modified` moves, the value is unchanged |
| field omitted | **silently ignored.** Same as null |

So the earlier framing was half right and needs correcting: `WhenWritingNull` is **not** the cause,
and removing the attribute would not fix anything — Elvanto treats an explicit `null` exactly like an
omission. A clear has to be expressed as an **empty string**, by the descriptor, deliberately.

Two consequences for this plan, both simplifying:

- **The "did the payload carry this field?" rule is unambiguous.** A field counts as carried only if
  it serialised to a non-null value. Null and omitted are behaviourally identical to Elvanto, so
  there is no third case to reason about, and the base-advance rule needs no special handling.
- **Clearing is a descriptor concern.** `ApplyToElvantoRequest` must map an app-side null to `""`
  for a field the app is genuinely clearing, and return `false` — not `true` — when it declines to
  set anything. Until it does, a clear is a no-op that reports success (**F8**), which is the bug as
  originally described; the diagnosis stands, only the mechanism was misattributed.

One caveat worth carrying: `""` clears, but nothing distinguishes *"the app is deliberately clearing
this"* from *"the app has no value for this"* at the descriptor boundary. Only the first should send
`""`. That is what the base makes answerable — `A ≢ B.app` with `A` null is a deliberate clear;
`A ≡ B.app` with both null is nothing to say.

## What deletes

`appChanged`, `elvChanged`, `elvFirstSeen`, `firstSeenAppWins`, the `IsValidInboundValue` mutation at
`:848-849`, the four-way branch chain, `holdFields`, `appWonConflictFields`,
`FieldProcessResult.HoldSnapshotFields`, `pushLanded` as a correctness signal, and
`UpdateSnapshotsAsync`'s independent second walk over the descriptor list. The base is written at
the point of resolution or not at all, so the two loops that disagree (**F10**, **F12**) become one.

## What is added

- `SyncEventType.Diverged` and a matching counter on `SyncResult`. Every `continue` and every
  direction refusal writes one, carrying both values. **This is what makes the reported symptom
  impossible to reproduce silently**, and it is the single highest-value addition in this document.
- `IsValidInboundValue` moves from mutating a fact to filtering a branch.
- `TranslateElvantoValue` must distinguish "Elvanto holds nothing" from "I could not translate
  this" — today `FirstOrDefault` conflates them and wipes a child's school grade (**F9**).

## Migration

One additive migration: `AppHash` and `AppValue` as nullable columns.

**Do not backfill `AppHash`.** A null app leg means "no base", so the first run after deploy
re-applies first-sync rules and surfaces every divergence that is invisible today — including the
whole restored-dump class in **F1**. That is the migration's most valuable side effect, and it is
why the first run must be a Decide-only run that somebody reads.

Renaming the table to `SyncFieldBases` is clearer but destructive; per
[GSBC.ImpactKids.Grpc/AGENTS.md](../../GSBC.ImpactKids.Grpc/AGENTS.md) that needs asking first. Propose it separately.

---

# Part 2 — Plan, then execute

## The shape

Every run splits into two phases that share one code path:

| Phase | Reads | Writes | Sends |
|---|---|---|---|
| **Decide** | Elvanto + app | the plan, the audit trail, pending reviews | nothing |
| **Apply** | the plan + live state | app people, bases, the audit trail | Elvanto |

Then the three modes stop being three code paths and become two calls:

- **Dry Run** = Decide, stop.
- **Execute** = Apply a plan the user has reviewed.
- **Full** = Decide, then immediately Apply the plan it just produced.

That last line is the point. Today `Full` and `DryRun` walk *different create paths* (`:482` versus
`:606-636`), so a dry run is a plan preview, not a rehearsal — it structurally cannot exercise
`SaveChanges`, the change interceptor, the payload builder, the `"new"`-family chain or the failure
branch. "Apply exactly what was shown" is not a meaningful promise until both walk the same code.

## The plan table

```
DbSyncPlannedChange
    Id
    SyncOperationId          -> FK to the operation that decided it
    PersonId?                 -- null for a create-from-Elvanto
    ElvantoId?
    Kind                      -- InboundField | OutboundField | CreateInElvanto
                              -- | CreateLocally | Archive | LinkPerson
    FieldName?                -- null for non-field kinds
    ObservedAppValue,     ObservedAppHash
    ObservedElvantoValue, ObservedElvantoHash
    ProposedValue
    Reason                    -- the resolver's own string, e.g. LastWriteWins:AppNewer
    Status                    -- Pending | Applied | Skipped | Stale | Failed
    StatusReason
```

The two observed hashes are not new machinery — they are the same base primitive at a different
moment. That is why this part is cheap once Part 1 exists.

## The sanity check at execute time

For every item, re-read both sides and compare against `ObservedAppHash` and `ObservedElvantoHash`.
If either has moved, the item is marked `Stale`, skipped, and reported. Nothing is clobbered on the
strength of a stale reading, which is the guarantee the whole feature exists to give.

**Apply executes only what the plan contains.** It never discovers new work. Anything that appeared
since Decide belongs to the next plan, and saying so plainly is what makes the button safe to press.

## Expiry

`DbSyncOperation.PlanExpiresAt`, defaulting to **four hours** and configurable. Past it, Apply
refuses the whole plan rather than any part of it.

The per-item staleness check is the real protection; expiry is a backstop against a different
failure it cannot catch. A stale item is one whose *values* moved. Expiry guards against the *set*
of items being wrong — people created, deleted or merged in Elvanto since Decide ran, which no
per-item check can see because those items are not in the plan. Four hours is long enough for a
considered review and short enough that the roll has not materially turned over.

## What this fixes on the way past

- Manual reviews get a real `SyncOperationId` FK, replacing the join-through-audit-rows hack that
  makes a review unreachable when its audit rows are lost (**F4**, **F11**).
- Decide writes nothing to `People`, so "DryRun" stops being a misnomer and the audit trail stops
  claiming local writes that never happened (**F12**).
- The long transaction spanning every HTTP call (**F15**) goes: Decide is a short read transaction,
  Apply sends outside a transaction and reconciles results in a short write transaction.

## UI

`Multiple.razor` gains an **Execute** action on an operation that has a non-empty `Pending` plan and
has not expired, plus a plan table on `Individual.razor` showing each item, its reason and its
status. The existing manual-review tab keeps its place between the two phases — that workflow was
already designed for this shape and needs no change beyond the FK.

---

# Part 3 — Splitting `ElvantoPersonSyncService`

1270 lines, and one of only four service classes in the project that are not `partial` — the other
three are 39, 43 and 344 lines, small enough that a single file is the right call. For context, the
largest partial file anywhere in `GSBC.ImpactKids.Grpc` is 273 lines.

The idiom is documented in
[GSBC.ImpactKids.Grpc/AGENTS.md](../../GSBC.ImpactKids.Grpc/AGENTS.md#partial-classes--the-house-idiom).
Partials are the right house answer and the right first move, but **they are not sufficient here**,
and it is worth being precise about why: partials split a file, not a method. `SyncAsync` is one
728-line method over fifteen shared mutable locals. No arrangement of files changes that.

So: both, in this order.

## Stage 1 — partials, free, no behaviour change

Make the class `partial` and move out the members that are already self-contained — each is
understandable from its parameters and the primary constructor alone, and none touches the shared
locals:

```
Features/People/Sync/Services/
    ElvantoPersonSyncService.cs            <- primary ctor, SyncAsync, the constants
    ElvantoPersonSyncService.Fetch.cs      <- FetchElvantoAsync, LoadAppPeopleAsync
    ElvantoPersonSyncService.Priming.cs    <- PrimeMedicalAllergyLookupsAsync, TranslateElvantoValue,
                                              MapMode, MapScope
    ElvantoPersonSyncService.Fields.cs     <- ProcessFieldsAsync, ApplyOutbound, UpdateSnapshotsAsync
    ElvantoPersonSyncService.Metadata.cs   <- UpsertMetadata, SaveNewPendingReviewsAsync
    ElvantoPersonSyncService.Creates.cs    <- CreatePersonFromElvanto and the create helpers
```

Roughly 500 lines relocated, mechanically, with `SyncAsync` left where it is. Safe to do on its own
and safe to do first.

## Stage 2 — collaborators, with Part 1

The length that remains is shared mutable state. Naming it is the fix:

| Type | Replaces | Why |
|---|---|---|
| `SyncPlanContext` | mode/scope re-derived at nine separate points (`:72`, `:113`, `:400`, `:482`, `:640`, `:792`, `:1194`, `:1266`) | `MayCreateLocalPeople`, `IsInScope(personId)`, `Commits` as first-class answers, so a future scope cannot forget to ask (**F6**) |
| `SyncWorkingSet` | the seven bare dictionaries at `:167-240`, four of them mutated through a 14-parameter signature | named mutators (`RecordLink`, `RecordCreatedFamily`) instead of pass-by-reference |
| `FamilyReconciler` | `familyIdMap`, `familyMembership`, `ResolveFamilyInElvanto`, `elvantoFamilyIdByLocal` — four structures resolving one correspondence by three different rules | one place to record a created family, instead of two paired writes at `:573-577` and `:955-959` |
| `FieldReconciler` | `ProcessFieldsAsync` + `UpdateSnapshotsAsync` | **this is where Part 1 lives** — one loop that decides and settles, so the two can no longer disagree |
| `PersonLinker` | `:248-378`, five bare `continue`s | returns `Linked \| CreatedLocally \| QueuedForReview \| DeniedPair \| OutOfScope`, making "denied" a value the caller must handle (**F3**) |
| `SyncReport` | four hand-written `SyncResult` literals (`:90-104`, `:142-156`, `:671-687`, `:745-759`) | one `Build()`; ~104 lines, and it is why `AuditLog` is populated on the success result and absent from all three failure results |

`FieldReconciler` is a pure function over (descriptor, app value, Elvanto value, base, config,
timestamps). That matters for verification — see below.

---

# Order of work

Each step builds and is independently revertible. **Nothing here enables a write to Elvanto.**

**Steps 1–5 have landed on `feature/bidirectional-elvanto-sync`. Step 6 has not, and needs asking
first.** What each step actually turned out to need is recorded under it.

1. **Stage 1 partials.** Mechanical, no behaviour change. Do it first so every later diff is
   readable. *Landed — the concatenated bodies were byte-identical to the original.*
2. **The two independent bugs**, neither of which waits on anything: the sticky `ManualReview`
   status that suppresses creates forever (**F3**) and the `UpsertMetadata` unique-index violation
   that fails whole runs (**F5**). Small, and the second can kill a run today. *Landed. F3 needed a
   third piece the plan did not name: denying a low-confidence match also has to release the
   outbound create, since per `docs/sync-feature.md` that denial means "these are two different
   people".*
3. **`SyncEventType.Diverged` + the counter.** Land this *before* the state change, on the current
   logic. It turns the existing silence into rows, which gives Part 1 a baseline to be compared
   against instead of a blank page. *Landed.*
4. **Part 1**, as `FieldReconciler` — the migration, the merge, the `bool` return on
   `ApplyToElvantoRequest`. Answer the null-clear question against Elvanto first. *Landed, plus one
   rule the design missed: **two sides that both say nothing are agreement, not divergence**. The app
   holding null against an Elvanto box reading "None" hashes as a difference and reported as one on
   every run forever — 89 rows on the first real run, in exactly the place the divergences are
   supposed to be a work-list. `AppHash` is deliberately not backfilled.*
5. **Part 2**, Decide/Apply split, plan table, staleness check, expiry, then the UI. *Landed. Two
   things the design did not settle:*

   - *Where the base of an **agreed** field is written. Decide has to do it — there is nothing to
     apply, and leaving it to Apply means a Decide-only run never settles anything and re-derives
     every field from first-sync rules forever. So Decide's writes are the plan, the divergences, the
     reviews **and** the agreements; still nothing in `People`.*
   - *The audit trail's tense. Decide writes only `Diverged` and `ManualReviewQueued`, both of which
     are true at decide time; Apply writes the past-tense rows. That closes **F12** exactly, at the
     cost of a dry run's stat tiles reading zero — so they now count pending plan items alongside
     audit rows.*
6. **Cleanups**, once the above is stable: `FamilyIdDescriptor.SetOnApp` inventing a Guid (**F7**),
   the unmapped school grade (**F9**), the split `try` around the review save (**F11**), the
   `Scope=Family` hole (**F6**), and the destructive migrations that need asking first — dropping
   `SyncFieldConfigs` (**F13**) and demoting `DbSyncMetadata` to an audit record (**F14**).

   *Landed. The sync feature has never been deployed — the production dump contains none of its
   tables — so the destructive migrations touch no production data.*

   *`DbSyncMetadata` is **dropped**, not demoted. The plan said demote because it still had two
   readers when it was written; those two were **F3** and **F5**, and step 2 removed both. Nothing
   read it after that. The link it stored is `DbPerson.ElvantoId` and the history is in
   `SyncAuditLogs` and the plan, so keeping it would have meant maintaining a third copy of facts
   two other tables already hold — which is what made it dangerous in the first place.*

   *The family oscillation left open by step 5 is closed here too, and it took four goes to find the
   real shape of it. In order: `TranslateElvantoValue` now excludes the asking person (the rule
   `ResolveFamilyInElvanto` already had, without which a person alone in their household mapped it
   straight back to the family they were already in); it ranks candidate families by membership
   rather than taking whichever the dictionary yielded first (397 people moved into a relative's
   family at random); a blank `family_id` is unreadable rather than empty; and **family is compared
   in the app's terms, not Elvanto's**. That last one is the substantive change — comparing in
   Elvanto's terms asks "which household does this person's local family mostly correspond to?", so
   a family split across two households disagreed with itself forever.*

   *Verified idempotent afterwards: Decide, Execute with writes off, Decide again produces **zero**
   family items. Before, the same 14 people were moved inbound and then planned straight back out.*

## Verification

There is no test project in the solution, and this is a state machine. That is the binding
constraint on all of the above.

**Add one, scoped narrowly.** `FieldReconciler` after Part 1 is a pure function with no database and
no HTTP — the nineteen-row truth table in the review doc is a test suite as written, and it is by
far the highest-value test surface in the feature. Add a second, trivial test asserting that
`desc.FieldName`, the EF property name and the seed string agree for all eleven fields; nothing
asserts that today, and a rename would silently and permanently break a field's sync.

**Then, against a fresh restore**, in this order:

1. `./db-restore.sh`, apply migrations.
2. A **Decide-only** run at `Scope=All`. Expect it to be loud: with no app-leg base, every field
   re-applies first-sync rules. Read the `Diverged` rows — **this is the run that finally shows the
   F1 backlog**, and it is the acceptance test for the whole exercise.
3. Re-run Decide. **Correction, from running it:** the second plan is *identical*, and that is the
   correct result rather than a finding. The expectation was written against a design where the
   first run settled snapshots for everything it had merely reported. It no longer does: agreements
   settle on the first run (18,683 bases), and what remains in the plan is genuinely outstanding
   work that no Decide-only run may consume. A second plan that shrank would mean step 4 was broken.
4. Edit one field in the app, run Decide, confirm it appears in the plan. Run Decide again without
   applying — **it must still be there.** That is F2, regression-tested.
5. Apply with writes still off. Every outbound item should report as suppressed, and its base must
   **not** advance. Run Decide again — the items are still there.

Only after all five does the question of enabling writes arise, and that follows the existing
dry-fire procedure in the Full-run handover, with one correction: that procedure currently consumes
pending changes (**F2**), and Part 1 is what makes it safe.

## What running it against production data turned up

Verification steps 1–5 all pass. Three things the design did not anticipate, all found by running
the sequence rather than by reading:

1. **Two sides that both say nothing are agreement, not divergence.** 89 rows per run. Fixed.
2. **An unresolvable app-side family is unknown, not empty.** 107 planned outbound *clears* of real
   people's Elvanto family, from people who were simply the only linked member of their household.
   With writes on this would have emptied 107 families. Fixed: `AppValueUnknown`.
3. **A blank Elvanto `family_id` is unknown, not empty.** The first attempt at (2) exposed it — 411
   planned inbound family moves, each of which would have minted a fresh Guid and put the person in
   a brand-new one-person household (**F7**'s mechanism). Fixed: `ElvantoValueUnknown`.

**Left for step 6, and it is a real finding:** applying an inbound family move and then planning the
reverse outbound move for the same 14 people in the next run. The person is moved into the local
family Elvanto says, and then the app's own grouping — read off their relatives — disagrees and
would push them back. It converges after one outbound rather than looping, and with writes off it
simply sits in the plan, but it means **family is the one field where Decide/Apply is not yet
idempotent**. It belongs with **F6** and **F7**, which are the family rework.

## The migration history was collapsed

The work went through **seven** migrations before it settled: `AddSyncTables`, two corrections to the
`SyncFieldConfigs` seed, a direction change, the base-value columns, the plan table, and finally a
migration dropping `SyncFieldConfigs` and `DbSyncMetadata` outright. Three of the seven corrected a
table the seventh deleted.

None had ever been deployed — the production dump's `__EFMigrationsHistory` ends at
`20260809062024_PlacementScoring`, and the dump contains none of the sync tables — so they were
replaced by a single `20260825155128_ElvantoSyncEngine`. Shipping a history that creates a table,
corrects its seed twice, and then drops it is a permanent record of a design that was changed before
anyone ran it.

Verified by comparing the schema built the two ways: **identical** columns, indexes and constraints
on every table. The only textual difference is column ordering, because `AppHash`/`AppValue` and
`PendingReviews.SyncOperationId` were `ALTER TABLE ADD` before and are now created inline.

## Out of scope

- Any change to the write guards, the budget or the allow lists.
- The merge feature for duplicate app records. The review rows it will read are unaffected by all of
  this.
- The Elvanto category and `family_relationship` gaps — real, recorded in the review as **not**
  having a mechanism, and a separate decision.
- Anything that enables a write. `AllowWrites` stays `false` for the entire duration of this work.
