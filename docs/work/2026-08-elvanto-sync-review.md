---
title: Elvanto sync — what the state model actually does
kind: discussion
status: accepted
opened: 2026-08-25
verified: 2026-08-26
code:
  - GSBC.ImpactKids.Grpc/Features/People/Sync
  - GSBC.ImpactKids.Grpc/Features/Sync
  - GSBC.ImpactKids.WASM/Features/Sync
---

# Elvanto sync — what the state model actually does

A four-lens review of the sync feature as it stands at `1ba30d1`, prompted by pending changes and
expected rows going missing. This doc is the **findings register**: what the code does, with
evidence. The response to it is
[2026-08-elvanto-sync-refactor.md](2026-08-elvanto-sync-refactor.md).

Line numbers are against `1ba30d1` and will drift. The mechanisms will not.

## One root cause, in four vocabularies

Four independent reviews, given different lenses and no knowledge of each other, landed on the same
thing:

| Lens | How it phrased the cause |
|---|---|
| State model | `appChanged` is derived from **a second clock** — an edit timestamp compared against a *poll* timestamp — instead of from a base value |
| Orchestrator | **Two loops walk the same descriptor list.** Decisions default to *skip*; the snapshot advance defaults to *yes*. The only bridge between them is a whitelist with one entry |
| Field layer | **Direction is entangled with change detection**, so a field the direction refuses is never compared at all |
| Silent loss | The engine has **no durable record of "this change is outstanding"**. The hold is built from a *reason* (a conflict) and handed to a writer that decides on a *fact* (did Elvanto's hash move) — unrelated conditions |

These are one finding. The three separately-diagnosed causes in the Full-run handover are not three
bugs; they are one missing concept, patched three times.

**The domain knowledge in this code is sound and hard-won.** The coverage guard, the
asker-exclusion family rule, the layered write budget, the additive medical/allergy round-trip and
`SyncContextAccessor` + the change interceptor are all correct, all load-bearing, and none of them
is the problem. The overcomplication is localised to one decision: deriving *"did the app change?"*
from a clock rather than from a stored base value.

## How the decision is made today

`ElvantoPersonSyncService.cs:813-860`:

```csharp
813  string? appValue = desc.GetFromApp(appPerson);
814  string? elvValue = desc.GetFromElvanto(elv);
815  string  appHash  = desc.Hash(appValue);
816  string  elvHash  = desc.Hash(elvValue);
818  snapshots.TryGetValue((appPerson.Id, desc.FieldName), out DbElvantoFieldSnapshot? snapshot);
822  bool elvChanged = snapshot is not null && elvHash != snapshot.LastSeenHash;
823  lastAppChange.TryGetValue((appPerson.Id, desc.FieldName), out DateTimeOffset appChangedAt);
824  bool appChanged = appChangedAt != default &&
825                    (snapshot is null || appChangedAt > snapshot.LastSeenAt);
...
844  if (appHash == elvHash) continue;
848  if (!desc.IsValidInboundValue(elvValue)) elvChanged = false;
853  if (!elvChanged && !appChanged && !elvFirstSeen) continue;
```

`appChanged` is not a tiebreak. It is an **admission gate**: a real, visible difference between the
two sides is discarded entirely unless a change-log row postdates the snapshot.

`lastAppChange` (`:175-179`) is a `MAX(ChangedAt)` over all history — a high-water mark, never
pruned and never acknowledged. `snapshot.LastSeenAt` is *when this app last polled Elvanto*. Those
two clocks are independent, and every way they drift makes a real divergence permanently invisible.

## The truth table

`S` = snapshot exists. `EH` = raw `elvHash != LastSeenHash`. `AT` = `appChangedAt` present and later
than `LastSeenAt` (or no snapshot). `V` = `IsValidInboundValue`. `D` = `config.Direction`.
`FP` = `FirstSyncPrecedence`. "Snapshot after" is what `UpdateSnapshotsAsync` (`:1026-1076`) does in
the same run — it walks **every** descriptor, ignores `config`, and skips only `holdFields` and a
null `elvValue`.

| # | S | EH | AT | V | D | app≡elv | Outcome | Audit row | Snapshot after | Verdict |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | any | any | any | any | Disabled | any | nothing (`:811`) | **no** | advances / created | pending app change consumed |
| 2 | any | any | any | any | any | **yes** | nothing (`:844`) | no | advances / created | benign |
| 3 | Y | N | N | any | any | no | nothing (`:853`) | **no** | unchanged | **stable silent divergence** |
| 4 | N | – | N | N | any | no | nothing (`:853`) | **no** | **created, `LastSeenAt = now`** | silent, and burns future `appChanged` |
| 5 | N | – | N | Y | Bi/In | no | inbound | `FieldUpdated` | created | first sync, Elvanto wins — intended |
| 6 | N | – | N | Y | Out | no | **nothing** | **no** | created | silent (no Out fields seeded) |
| 7 | Y | Y | N | Y | Bi/In | no | inbound | `FieldUpdated` | advances | correct |
| 8 | Y | Y | N | Y | Out | no | **nothing** | **no** | **advances** | Elvanto's change discarded, marked seen |
| 9 | Y | Y | N | N | any | no | nothing | no | **advances** | Elvanto cleared it; app keeps old value, silently |
| 10 | Y | N | Y | any | Bi/Out | no | outbound | `Pushed`/`WouldPush` | **not advanced** | correct — self-protecting |
| 11 | N | – | Y | N | Bi/Out | no | outbound | `Pushed`/`WouldPush` | **created** | **invisible forever if push does not land** |
| 12 | N | – | Y | Y | Bi/Out | no | outbound | `Pushed`/`WouldPush` | **created** | same, **and `FirstSyncPrecedence` bypassed** |
| 13–14 | N | – | any | Y | Bi/Out | no | outbound `MergeForFirstSync` | `Pushed`/`WouldPush` | **created** | **documented first-sync merge destroyed by one writes-off run** |
| 15 | Y | N | Y | any | In | no | **nothing** | **no** | not advanced | silent; change survives to next run |
| 16 | N | – | Y | Y | In | no | **nothing** | **no** | **created** | app change consumed *and* Elvanto's value not applied |
| 17 | Y | Y | Y | Y | Bi | no | `ConflictResolver` | `Conflict` | held iff app won and `!pushLanded` | intended |
| 18 | Y | Y | Y | Y | In/Out | no | **nothing** | **no** | **advances** | **both sides' changes discarded, divergence marked seen** |
| 19 | Y | Y | Y | N | any | no | outbound | `Pushed`/`WouldPush` | **advances** | app change consumed if push does not land |

**Nine of nineteen rows end in no action, no audit row and no counter.** There is no
`SyncEventType` capable of expressing "these two differ and I chose not to act" — the enum is
`Match, FieldUpdated, Conflict, Created, PushedToElvanto, WouldPushToElvanto, WouldCreateInElvanto,
ManualReviewQueued, Archived`. Silence is structurally indistinguishable from "nothing to do", which
is exactly the reported symptom.

## Findings, ranked

### F1 — On a restored dump, every app value Elvanto lacks is invisible. Permanently.

Two facts meet. `FieldChangeLogs` did not exist before this branch, so every edit
predating it — the entire production dump — has no row and `appChanged` is false forever. And
`UpdateSnapshotsAsync:1043` (`if (elvValue is null) continue`) means a field Elvanto holds nothing
for **never gets a snapshot created either**.

So: app has a phone number, Elvanto's is blank. Past `:844` (values differ), `elvChanged` false (no
snapshot), `elvFirstSeen` false (`elvValue is null`), `appChanged` false (no log row) →
`:853 continue`. Identical on every run, forever, with no audit row. The run reports zero outbound
fields, which reads as *"nothing to push"*.

This is almost certainly the largest source of the reported missing rows, and it is invisible
precisely because it looks like success.

It also silently disables the documented first-sync medical behaviour:
`MedicalAllergyNotesDescriptor.GetFromElvanto` returns null for an empty box, so for any child whose
Elvanto medical field is blank, `firstSeenAppWins` is unreachable and structured allergy rows are
never pushed at all.

### F2 — The documented dry-fire procedure consumes pending changes

The handover's safe-write recipe is a **Full** run with `MaxWrites: 0`. A Full run commits
(`:645-649`; only `DryRun` rolls back). For rows 11, 13–14 and 19 it creates snapshots with
`LastSeenAt = now`, permanently burying the very changes it just reported as `WouldPushToElvanto` —
an event that reads as a promise they will push next time. They will not.

Jocelyn Lukey's pending email change survives only because `Email` landed in row 10, the
self-protecting case. It is pending by luck, not design.

### F3 — Denied reviews permanently and silently suppress the outbound create

`LastSyncStatus = ManualReview` is set at `:340` and **nothing ever resets it**. `UpsertMetadata` on
an existing row touches only `MatchConfidence` and `MatchStrategy` (`:1090-1091`); the reset at
`:392-393` sits inside the linked-person loop that a review candidate never reaches;
`ApproveReview`/`DenyReview` touch only `DbSyncPendingReview`.

The create loop then skips the person forever, with a bare `continue` and no audit row:

```csharp
488  if (metaByElvantoId.Values.Any(m => m.PersonId == local.Id && m.LastSyncStatus == SyncStatus.ManualReview))
489      continue;
```

Per `docs/sync-feature.md`, denying a **duplicate** means "create them as a separate person" and
denying a **low-confidence match** means "never link this pair" — neither means "never create this
person". The allow-list skip six lines later does write an audit row; this one does not.

### F4 — `ManualReviewQueued` audit rows are rendered by no table on any page

`Individual.razor.cs:36-48` partitions the three audit tabs on `Direction`. All four
`ManualReviewQueued` writers pass no direction, so `Direction` is null — which excludes them from
the first two tabs — and the third excludes them by name:

```csharp
46  .Where(x => x.Direction == null && x.EventType != SyncEventType.ManualReviewQueued)
```

Their only surface is the Manual Review tab, gated on a matching `DbSyncPendingReview` row being in
the client store. The stat tile counts the audit rows, not the reviews (`:330`), so a run whose
review save failed shows *"Manual Review: 12"*, no tab, and twelve rows that no table renders. A
number you cannot click on.

### F5 — `UpsertMetadata` can fail an entire run

`DbSyncMetadata` is unique on `ElvantoId` **and** on `PersonId` (`GsbcDbContext.SyncModel.cs:36-47`),
but `UpsertMetadata` looks up by `ElvantoId` only (`:1088`). The low-confidence path writes a row
for an unlinked person against someone else's Elvanto id (`:337`); when that person later links to
their real id, no row is found, `AddAsync` runs (`:1105`), and a second row with the same `PersonId`
violates the index → `SaveChanges` throws → the whole sync fails.

This is the bug class `AllowMultipleReviewsPerPerson` fixed for `DbSyncPendingReview`, still live
one table over.

### F6 — `Scope=Family` shares the `Scope=Person` hole, and a scoped dry run is already destructive

`GetPersonInfo.cs:53-58` pulls the whole Elvanto roll whenever any family member is unlinked, while
`LoadAppPeopleAsync` (`:1267`) loads only that family. Nothing between `:248` and `:266` consults
`request.Scope`, so the create branch makes a local person for every non-matching row — the same
~1718 as the documented `Scope=Person` case.

And it is not merely latent: the create branch calls `audit.Log` (`:270`), and audit rows persist
outside the transaction. **A `Scope=Person` dry run on an unlinked person already writes ~1718 audit
rows that survive the rollback.**

### F7 — `FamilyIdDescriptor.SetOnApp` invents a family

```csharp
// FamilyDescriptor.cs:35-36
person.FamilyId = Guid.TryParse(value, out Guid g) ? g : Guid.NewGuid();
```

`IsValidInboundValue` is not overridden, so it inherits `=> true`. An Elvanto person who loses their
family reads as null, the inbound arm fires, and the person is moved into a **brand-new one-person
household**. `UpdateSnapshotsAsync` then skips the field (null `elvValue`), so the snapshot never
advances and it **recurs on every run**.

### F8 — A null clear cannot be sent, and is audited as a success

Every optional property on the outbound request is
`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
(`ElvantoUpdatePersonRequest.cs:23-39`, `ElvantoPersonFields.cs:9-26`). Clearing Email, Phone,
DateOfBirth, FirstTime, MediaConsent or MedicalAllergyNotes in the app builds a request carrying
only `{"id":"…"}`, Elvanto answers `ok`, `pushLanded` is true, the hold is released and the snapshot
advances. Audited once, as `PushedToElvanto`.

`MediaConsentDescriptor.cs:31` (`if (value is null) return;`) declines even more explicitly.

**Verified against the live API, 2026-08-26** (see the refactor doc for the full table): Elvanto
treats an explicit `null` exactly like an omitted field — both are silently ignored, answered `ok`.
An empty string `""` clears, on every field type including the `select`, `datepicker` and `textarea`
custom fields. So `WhenWritingNull` is not the cause and removing it would change nothing; a clear
has to be sent as `""` by the descriptor, deliberately.

### F9 — An unmapped Elvanto school grade silently clears the child's grade

`elvChanged` is computed on the raw id at `:816-822`, *before* `TranslateElvantoValue` at `:827`.
`TranslateElvantoValue:1177-1178` uses `FirstOrDefault`, which returns null for a grade with no
`DbSchoolGrade` row — so "I don't recognise this" becomes "Elvanto holds nothing", the inbound arm
fires, and `SchoolGradeId` is set to null. An audit row is written, but it reads as a legitimate
clear.

### F10 — Direction is one-way in a way that discards both sides

`SchoolGradeId` and `FamilyGuardian` are seeded `InboundOnly`. With an app-side edit and no Elvanto
change, no branch matches: `:862` needs `!appChanged`, `:874` needs `Bidirectional or OutboundOnly`,
`:901` needs `Bidirectional`. Nothing is logged and nothing is counted.

Worse, when **both** sides changed: the same three misses apply, so Elvanto's inbound change is not
applied either — on a field whose whole declared point is that Elvanto wins — and because
`elvChanged` is true the snapshot advances anyway. Both changes gone, nothing recorded.

### F11 — Pending reviews are lost on any flush failure, and the run still reports success

```csharp
654  try { await audit.FlushAsync(operation, token);
656        await SaveNewPendingReviewsAsync(newPendingReviews, token); }
658  catch (Exception flushEx) { logger.LogWarning(flushEx, "…failed to persist audit logs", operationId); }
```

One `try`, one `catch`, logged at Warning as *"audit logs"*. If `FlushAsync` throws,
`SaveNewPendingReviewsAsync` never runs and every review from that run is discarded, while the
method still returns `Success = true` with a non-zero `ManualReviewQueued`. `newPendingReviews` is
also declared *inside* the `try` (`:242`), so the outer `catch` at `:689` cannot flush it either.

### F12 — "DryRun" is not read-only, and the audit trail lies about local writes

A dry run writes to `SyncOperations`, `SyncAuditLogs` and `PendingReviews`. Because audit rows are
flushed outside the transaction, `Created / NewFromElvanto` (`:270`), `Match / AutoLinked` (`:284`),
`FieldUpdated / InboundFromElvanto` (`:864`) and `Archived / RemovedFromElvanto` (`:415`) all
persist in the **past tense** from a run that did none of them. Only the outbound branch
distinguishes (`WouldPushToElvanto`). The handover treats the audit tables as authoritative; for
local effects they are not.

### F13 — `DbSyncFieldConfig` buys nothing

All eleven rows match their descriptor's `DefaultDirection` exactly. Nothing reads or writes the
table at runtime — no settings UI, no gRPC method, no admin page; the WASM project does not
reference `SyncDirection` at all. Its behavioural contribution today is zero, and two corrective
migrations were needed to get it there, one of which cost a family move dropped with no audit row.

`PrecedenceOnTie`, the other column, is unreachable in practice: `ConflictResolver.cs:37` is guarded
by two earlier returns, and reaching it needs an app edit and an Elvanto edit in the same second.

### F14 — `DbSyncMetadata` is 8/10 write-only

`LastSyncAt`, `MatchConfidence`, `MatchedAt`, `MatchStrategy` and `ManualReviewReason` are never read
back from the database on a later run; the UI reads the equivalents off `DbSyncPendingReview`. The
link it stores already lives on `DbPerson.ElvantoId`. Only `PersonId`/`ElvantoId`/`LastSyncStatus`
are read, and that read is F3 and F5.

### F15 — No concurrency control anywhere

No advisory lock, no in-flight flag, no concurrency token on `DbPerson`. Two syncs at once race on
the unique `ElvantoId` index (turning a race into a whole-run failure) and, with writes on, can
create the same person in Elvanto twice below the budget ceiling. A user edit landing after
`lastAppChange` is read (`:175`) but before snapshots are written is not deferred to the next run —
it is buried by that run's own snapshot write.

Related: the transaction is held open across every HTTP call (`:59` → `:649`), and Elvanto is not
transactional. Every rollback undoes half the world.

## What the read path is clear of

Stated because "I looked and found nothing" is worth as much as a finding.

- **No `Take`, limit or page cap drops rows.** `RefreshableStore.RetrieveEntities` sends
  `BasicReadMultipleRequest.All()`, which disables pagination; `ReadAuditLogs` and
  `ReadPendingReviews` do not paginate at all; `ReturnInBatches` yields every batch.
- **No grouping or `DistinctBy` collapses rows.** The only `Distinct()` is over field names for a
  filter dropdown.
- **The store-seed idiom is correctly applied on both pages**, including the `Multiple.razor.cs`
  ordering, which looks wrong against `frontend-store-architecture.md` but is not — the stores are
  app-wide singletons, so a cache hit implies an earlier successful call.
- **Converters lose nothing**, and all eleven descriptor `FieldName` values match `DbPerson`
  property names exactly.
- **`SyncContextAccessor` is safe under concurrency** — `AsyncLocal`, so a concurrent person edit
  during a sync is not mislabelled `Source = Elvanto`.

## What is essential and must survive any rewrite

- `RetrieveElvantoPeople` returning the whole roll or throwing, and the 90% coverage abort. Absence
  is read as deletion and six tables cascade; a dropped page once archived 726 children.
- `ResolveFamilyInElvanto` excluding the asker. Subtle, correct, and the most valuable prose in the
  file.
- The layered write budget and allow lists, above the single `HttpClient` choke point.
- The `MedicalAllergyFormat` round-trip: additive inbound, unparseable text parked verbatim in an
  "Other" note, `SetOnApp` throwing rather than writing with unprimed lookups.
- `SyncContextAccessor` + `FieldChangeTrackingInterceptor`, which is the only correct way to tell an
  app edit from the sync's own writes.
- The `pushPossible` / `willSend` distinction that makes an audit row say "would push".

## The constraint on fixing any of it

**There is no test project in the solution.** Three independent authorities have to agree on every
field name — `desc.FieldName`, the seed's `Cfg("…")` string, and EF's property name in the change
interceptor — and nothing asserts it. Renaming `DbPerson.FirstTime` would silently and permanently
break that field's outbound sync, with no error anywhere.
