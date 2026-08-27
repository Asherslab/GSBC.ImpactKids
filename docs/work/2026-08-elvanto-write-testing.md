---
title: Elvanto sync — full-feature test, ending in real writes
kind: handover
status: open
module: sync
opened: 2026-08-27
verified: 2026-08-27
code:
  - GSBC.ImpactKids.Grpc/Features/People/Sync
  - GSBC.ImpactKids.Grpc/Features/Elvanto/ElvantoServices
  - GSBC.ImpactKids.WASM/Features/Sync
---

# Elvanto sync — full-feature test, ending in real writes

For whoever runs this next. The read and decide halves are built and verified; what has never happened
is a real write to Elvanto. This doc is the plan for testing the whole feature end to end and then,
last, letting a small number of writes actually leave the process.

Read [2026-08-elvanto-family-mapping.md](2026-08-elvanto-family-mapping.md) and
[2026-08-elvanto-sync-refactor.md](2026-08-elvanto-sync-refactor.md) first. This assumes both.

---

## THE WRITE GATE — read this before touching any config

### Two rules, and the second one has no exceptions

**1. You must halt before enabling writes, and produce a written verification of exactly what will be
sent, for a human to approve.** Not a summary. Not "the plan has 67 outbound fields". The actual
people, the actual current Elvanto values, the actual proposed values, and the actual JSON bodies.
**The verification must be produced from the plan that is about to be executed** — decided after any
apply that has happened since, not carried over from an earlier run. Point 1 below says why.

**2. The sync engine is the only thing that may ever write to Elvanto.** Asher's instruction,
2026-08-27. You do not create, edit or delete anything in Elvanto yourself — not by `curl`, not by a
script, not through the Elvanto UI, not "just to set up a test". If a record needs to exist there to
test against, **ask Asher to make it** and wait.

That second rule is not a subset of the first. The gate below governs writes the *engine* performs;
rule 2 says there is no other kind. An ad-hoc `people/create` to seed a fixture would bypass every
mechanism this doc describes — the allow lists, the budget, the audit trail, the plan — and it would
be indistinguishable in Elvanto from a real one. Read-only calls are fine and encouraged; see
"Reading the result".

This is not ceremony. Three things make it necessary:

1. **The plan is decided with writes off and executed later.** `SuppressionReason` is evaluated in
   *Apply*, not Decide, so a plan sitting in the database right now — 71 `OutboundField` rows and 28
   `CreateInElvanto` rows — becomes live the moment the config flips.

   **What is not true is that any plan you happen to be looking at is the plan that would go.** This
   doc used to say so, and it was wrong: an apply changes the app, and a run decided after it can
   legitimately decide differently. A measured case — an inbound medical/allergy write parses
   Elvanto's free text into rows and the app then composes back something that is not the same
   string, which is a real difference the next run has to say something about. So the rule is about
   *which* plan, not about plans being immutable: **approve the plan that the execute will run, and
   re-decide it after any apply.** A plan whose operation has already been executed, or that predates
   an apply, is not the plan that would go and may not be used as the verification.

   The 2026-08-27 finding behind that correction was also a genuine bug, now fixed: a silent
   inbound refusal settled the base as though the app held Elvanto's value, and the next run read the
   gap as an app-side edit and planned to push it — the outbound surface grew 67 → 73 across an
   apply. It no longer does; 71 before and after, verified on a fresh restore. Do not treat that fix
   as making a stale plan safe to approve.
2. **The allow lists are the containment, not the plan.** The plan itself does nothing to restrict
   *who* is written. Only `AllowedUpdatePersonIds` / `AllowedCreatePersonIds` do. Mode and scope used
   to be listed here too; both were removed on 2026-08-27, which changes nothing about this rule —
   they never contained anything either.
3. **Creates are irreversible from here.** `people/create` mints a person and a family in the
   church's live Elvanto. There is no delete in this codebase. A wrong create is cleaned up by hand,
   in Elvanto, by a person.

### The halt, concretely

Stop and hand over a verification containing, per person to be written:

| What | Where it comes from |
|---|---|
| App person id | `PlannedChanges.PersonId` — this is what goes in the allow list |
| Elvanto id | `PlannedChanges.ElvantoId` |
| Field | `PlannedChanges.FieldName` |
| What Elvanto holds **now** | re-read live from `people/getInfo`, **not** from `ObservedElvantoValue` |
| What would be written | `PlannedChanges.ProposedValue` |
| The exact body | `ObservedAppValue` for a create; `DescribePayload` for an update |

Re-reading the live value matters: `ObservedElvantoValue` is what the run *observed when it decided*,
which may be hours old. Apply re-reads and refuses stale items, but the human approving the write
should be looking at what is true now, not at what the plan remembers.

Then **stop and wait for explicit approval.** Do not flip config in the same turn as producing the
verification. Do not treat "yes that looks right" about a *plan* as approval to *write* — approval
must name the write.

### Only then

1. Set the allow lists to the test people's **app person Guids** and nothing else.
2. Set `MaxWrites` to the exact number of writes approved.
3. Restart the AppHost (config is read at startup).
4. Execute.
5. Verify against Elvanto by reading it back, and report what actually happened.

---

## Where things stand, 2026-08-27

Verified against the database, not remembered. Re-measured at 04:4x after the inbound-refusal fix
below; the earlier 03:1x figures are superseded and differed only in the four rows that moved from
inbound to outbound.

The dev database was restored from the production dump, one DryRun ran, the plan was executed, and
two more DryRuns settled it.

| | On a fresh restore + one DryRun | After Execute |
|---|---|---|
| Live people | 1772 | 1767 |
| `ElvantoFamilyLinks` | 511, seeded by that run (506 `Seeded`, 5 `Observed`) | 511 |
| Plan rows | **565, all `Pending`** | 466 `Applied`, 99 `Skipped`, 0 `Failed` |
| `OutboundField` | **71** | **71** on each settling DryRun — stable |
| Pending manual reviews | 18 | 18 |
| Divergences | **0** | 2 (`MedicalAllergyNotes`, `BaseDisagreesWithBothSides`) |
| The bucket `b1680e5d-…` | 412 | **3** |
| People with no family | 0 | **399** |

Nothing reached Elvanto: 71 `WouldPushToElvanto` and 28 `WouldCreateInElvanto` audit rows, zero
`PushedToElvanto`.

Writes are off: `AllowWrites`, `AllowCreates`, `AllowUpdates` all `false` in
`GSBC.ImpactKids.Grpc/appsettings.Development.json`, and no allow lists or `MaxWrites` are set.

### What this plan would send outward

The whole outbound surface, and it is small:

| Kind | Count | Detail |
|---|---|---|
| `OutboundField` | **71** | 60 × `MedicalAllergyNotes`, 7 × `MediaConsent`, 3 × `FirstTime`, 1 × `DateOfBirth` |
| `CreateInElvanto` | **28** | 26 `:NewFamily`, 2 into an existing household |

The four `FirstTime` / `DateOfBirth` rows are new as of 2026-08-27 and are the *first* run's answer
now, not a second run's. They are the restored-dump shape — the app holds a date Elvanto has never
been told about — and they used to be planned inbound as a clear that the descriptor then silently
refused. `FirstSync:ElvantoHasNothing` is the branch that exists for exactly this, and it is now
reachable.

No outbound `FamilyId` at all — a deliberate property, see the family-mapping doc.

The old `test user - 1…4` records were deleted after the previous round and are gone from both sides.
**There are no test accounts. Making them is the first task**, before any of the phases below.

---

## Config reference — what actually stops a write

Establish this from the code, not from this table, if anything looks off. But this is what it says
today.

| Setting | Effect | Enforced where |
|---|---|---|
| `Elvanto:AllowWrites` | Master switch. False and **nothing below the transport gate runs for a mutation**, whatever the calling code does | `ElvantoService.SendMessage` |
| `Elvanto:AllowCreates` | `people/create` specifically. Beneath `AllowWrites` | `RefuseMutation` |
| `Elvanto:AllowUpdates` | `people/edit` specifically. Beneath `AllowWrites` | `RefuseMutation` |
| `Elvanto:AllowedCreatePersonIds` | **App person Guids** that may be created. Empty = no restriction | orchestrator, `Apply.cs:435` |
| `Elvanto:AllowedUpdatePersonIds` | **App person Guids** whose fields may be pushed. Empty = no restriction | orchestrator, `Apply.cs:304`/`540` |
| `Elvanto:MaxWrites` | Hard ceiling on mutations **per process**, never replenishes | transport, `RefuseMutation` → `ElvantoWriteBudget` |

Two things about this that are easy to get wrong:

- **The allow lists are orchestrator-level; `MaxWrites` is the transport backstop.** They are not the
  same mechanism and they fail differently. Set *both*: the lists say who, the budget says how many,
  and the budget cannot be talked round by any bug in the layer above it.
- **The allow lists take app person Guids, not Elvanto ids.** Putting an Elvanto id in one silently
  matches nobody, which reads as "everything was suppressed" and looks like success.

Empty list means **no restriction**, not "nobody". An empty allow list with writes on writes
everybody.

---

## Test accounts — make these first

Confirmed by Asher on 2026-08-27: the previous round's test people were deleted, from Elvanto and
locally. Nothing is reusable. The two write paths need different things, so make both:

| To test | You need | Who makes it |
|---|---|---|
| **Update** (`people/edit`) | a person in both systems, **linked** | **Asher** creates them in Elvanto. You then run a full DryRun+Execute so the sync pulls them in and links them, and edit a field *in the app* to make something pending |
| **Create** (`people/create`) | a person in the app only, **unlinked** | **you**, through the app's Create Person flow — that is a local write, not an Elvanto one. Do not sync them until you mean to |

The split follows rule 2: anything that has to exist *in Elvanto* is Asher's to make, and you say
what you need rather than making it. Anything local is yours.

The app person Guid is what goes in the allow list, for both. Get it from `People.Id` — not the
Elvanto id.

### Name them so the matcher cannot mistake them for anyone real

`PersonMatcher` scores every unlinked app person against every Elvanto record, and ≥80 auto-links
with no human involved:

| Score | Rule | Result |
|---|---|---|
| 100 | exact first+last **and** matching DOB | auto-links |
| 90 | fuzzy name (Levenshtein ≤2 on **both** parts) **and** matching DOB | auto-links |
| 75 | exact first+last, no DOB | manual review |
| 50 | email match | manual review |

Two consequences worth planning around:

- **Exact name alone is 75, which is review, not auto-link.** If you want a test person to link
  cleanly, give them the *same DOB* on both sides. If you want to test the review queue instead,
  give them a name match and no DOB.
- **Levenshtein ≤2 is a wide net on short names.** "Amy"/"Amie" is inside it, and with a matching DOB
  that is a silent auto-link to a real child. Use long, obviously-fake, distinctive names —
  `Zzztestperson Writetest` rather than `Test User` — and a DOB no real child has.

### Cleaning up afterwards, on both sides

`db-restore.sh` resets the local database and **does nothing to Elvanto**. Every test write survives
every local restore.

- **Asher** deletes the test people in Elvanto. Hand over the list of ids; do not delete them
  yourself.
- **Asher** deletes the family the create minted. A create with `family_id: "new"` makes a household
  as well as a person, which is easy to leave behind — so name it explicitly in the list you hand
  over, rather than assuming the person deletion covers it.
- **Then delete the `ElvantoFamilyLinks` row that create wrote** (`Source = CreatedInElvanto`). This
  is the one that will bite silently: both columns are unique, so a row pointing at a retired Elvanto
  family id permanently occupies that side of the mapping, and a later legitimate pairing for the
  same local family is refused with no obvious cause.

  ```sql
  select * from "ElvantoFamilyLinks" where "Source" = 'CreatedInElvanto';
  ```

- A test person deleted in Elvanto but left locally will be read as removed from the roll and planned
  for `Archive` on the next full sync. Expected, but do not mistake it for a bug.

## Suggested test order

Reads and decides first, writes last and smallest. Each phase should be verified from the database
or from Elvanto, not from the logs — the gRPC log buffer is saturated within seconds.

### 1. Read and decide (no writes, no local mutation)
- Press **Decide Plan**. There is no mode or scope to choose any more — every run decides the whole
  roll and writes nothing. Confirm it reproduces: 565 plan rows, 71 outbound, 0 diverged, 511 links.
- Run it twice. The numbers must be identical and the link table must not grow.

### 2. Local apply (writes still off)
- Execute the plan. Expect ~466 applied, ~99 suppressed, 0 failed; bucket 412 → 3; no-family 0 → 399.
- Then decide again: the family work should be settled and not re-proposed.
- **Check the outbound count across the apply.** 71 before, 71 after, and a third decide identical to
  the second. A grown outbound surface means a base was settled on a write that did not fully land;
  that is the 2026-08-27 defect and it is what the check is for. Two `MedicalAllergyNotes` rows do
  legitimately move to `Diverged: BaseDisagreesWithBothSides` after the apply — the app parsed
  Elvanto's text into rows and composes back something different, and the base says so rather than
  inventing an edit.
- Check the UI: `/Sync` operation page, person page, attendance family page, sign-in wizard.

### 3. Manual review — verified 2026-08-27

18 pending. Approve one, deny one, re-run. **Only deny changes an outcome**, and that is by design,
not a gap:

| Decision | What it means | What happens |
|---|---|---|
| **Deny** — "different people" | releases the create | the person gains a `CreateInElvanto` plan row and is never matched against that Elvanto record again. `CreateInElvanto` went 28 → 29 |
| **Approve** — "same human" | keeps the create **suppressed** | no plan row, no link, nothing to apply. Audited as `ManualReviewQueued: PotentialDuplicate:AlreadyLinkedInElvanto` |

**Approving does not link, and must not.** Two app people cannot share one `ElvantoId`, so approving
says "yes, same human" and leaves the duplicate suppressed; merging the two app records is a
separate, manual job. This is stated at `Creates.cs:133-136`. An earlier version of this doc said to
"confirm an approved pair links" — there is no such behaviour to confirm.

These 18 rows are **not** `PersonMatcher` scores. They are raised by the create path's duplicate
check (`Creates.cs:108`) when an unlinked app person shares an exact name with an already-linked
one, and `MatchConfidence = 50` is a hardcoded literal for that strategy (`Creates.cs:153`). Do not
read them against the matcher's 100/90/75/50 table below — it describes a different mechanism.

### 4. Scoped runs — **phase deleted; the scopes no longer exist**

Removed from the codebase on 2026-08-27, along with `ElvantoSyncScope`, `PersonId`/`FamilyId` on the
request and on `DbSyncOperation`, and the two guards that existed only to contain them
(`SyncWorkingSet.MayCreateLocalPeople`, and the scope check in `DecideArchives`). Every run is the
whole roll. Use the allow lists to narrow a run's *effect*, which is what the write phase does anyway.

See "Scoped runs were broken" under Traps for the mechanism and the evidence — kept because it is
also the reason `GetPersonInfoAsync` still returns null on the write path.

### 5. Writes — both rules above apply

Prerequisite: the test people exist. The Elvanto-side one is Asher's to create — ask, and wait,
rather than seeding it yourself.
Smallest first, one kind at a time, each with its own halt and approval:

1. **One update.** `AllowWrites=true`, `AllowUpdates=true`, `AllowCreates=false`,
   `AllowedUpdatePersonIds=[<one test person>]`, `MaxWrites=1`.
2. **One create.** Creates on, updates off, `AllowedCreatePersonIds=[<one test person>]`,
   `MaxWrites=1`. This also creates a family in Elvanto — expect `family_id` back on the response and
   an `ElvantoFamilyLinks` row with `Source=CreatedInElvanto`.
3. **A small batch**, only after both singles are verified by reading Elvanto back.

Between each: restart the AppHost. `MaxWrites` never replenishes within a process, which is the
point, so a second write attempt in the same process is *supposed* to be refused.

---

## Traps

Carried forward because each one has already cost time.

- **Scoped runs were broken, and were removed on 2026-08-27 rather than repaired.** Kept in full
  because the cause below still affects the write path, and so nobody reintroduces scoping without
  reading it.

  **The cause is one line.** Elvanto's `people/getInfo` returns `"person": [ {...} ]` — an *array*.
  `ElvantoGetPersonInfoResponse.Person` is declared as a single `ElvantoPerson?`
  (`Models/ElvantoGetPersonInfoRequest.cs:28-29`), so the deserialize at `ElvantoService.cs:118`
  throws, the `catch` at line 126 logs a **warning** and returns `default`, and the caller sees
  "no such person". The HTTP call itself is a clean 200. Observed exception:

  ```
  System.Text.Json.JsonException: The JSON value could not be converted to
    …Models.ElvantoPerson. Path: $.person
    at …ElvantoService.SendMessage[TRequest,TResponse](…) ElvantoService.cs:line 118
  ```

  **What each scope actually does**, measured on the fixed build against the real Elvanto account:

  | Scope | Case | Result |
  |---|---|---|
  | `Person` | linked person | `Success`, **0 processed, 0 planned** — silently does nothing |
  | `Person` | unlinked person | falls back to the whole roll so the matcher can run — the trap below |
  | `Family` | every member linked | `Success`, **0 processed, 0 planned** — silently does nothing |
  | `Family` | any member unlinked | falls back to the whole roll: 1721 processed, not the family |

  The failure is the same in every row — every `getInfo` returns null. It is only *masked* when a
  scope happens to hit the full-roll fallback (`GetPersonByIdOrSearchAsync` line 30,
  `GetPeopleForFamilyAsync`'s `hasUnlinked` branch), and then the run is not scoped at all. So a
  family-scoped run is broken precisely when it looks safest: a fully-linked family, where the
  fallback never fires.

  This is the same signature as the `family`-field trap below — a zero-person run reported as
  `Success` with an empty plan. It also supersedes the old "person-scoped runs skip `FamilyId` and
  nobody has explained why": they skip everything, and this is why.

- **The unlinked-person fallback wrecked the database — resolved by the removal.** `Scope=Person` on
  an *unlinked* person fetched the whole Elvanto roll so the matcher could run, then created a local
  person for every row that did not match — ~1718 spurious people. Local only, no Elvanto writes, but
  it was a restore to undo. This was the strongest reason the scopes went.
- **Never add contacts to the main roll.** Absence from it drives archiving; a short roll once
  archived 726 children. `LoadWorkingSetAsync` has two independent refusals — leave them alone.
- **Never ask Elvanto for the `family` field.** Silently ignored on `getAll`; on `getInfo` it breaks
  the call so the sync processes zero people and still reports `Success` with an empty plan.
- **The stale-assembly trap.** `build_solution` can report success without recompiling. Check the
  literal reached the binary before trusting a result (.NET stores strings UTF-16):
  ```bash
  python3 -c "print(open('GSBC.ImpactKids.Grpc/bin/Debug/net10.0/GSBC.ImpactKids.Grpc.dll','rb').read().count('YourLiteral'.encode('utf-16-le')))"
  ```
- **A WASM change needs a full stop-and-restart**, not a rebuild. So does any config change.
- **Console errors survive a failed load.** After a fix, clear the buffer *and* reload before
  concluding it is still broken — six identical exceptions with the same timestamp are one stale
  load, not six failures.
- **A razor `@* comment *@` between component attributes** is parsed as an attribute name. Builds
  clean, throws at render. Comments go above the tag.
- **`db-restore.sh` does not clean Elvanto.** Anything a write test creates there survives every
  local restore and has to be removed by hand, in Elvanto.
- **`GsbcDbContextFactory` hardcodes port 60536** while the container keeps whatever port it was
  created with. `db-restore.sh` passes the discovered port; plain `dotnet ef` does not.

---

## Reading the result

From the database, not the logs:

```bash
c=$(docker ps --format '{{.Names}}' | grep '^sql-')
docker exec -e PGPASSWORD="$(docker exec $c printenv POSTGRES_PASSWORD)" $c psql -U postgres -d impact-kids -c \
  'select "EventType", "Reason", count(*) from "SyncAuditLogs"
   where "SyncOperationId" = (select "Id" from "SyncOperations" order by "StartedAt" desc limit 1)
   group by 1,2 order by 3 desc;'
```

A suppressed write is audited with the reason that stopped it — `Elvanto:AllowWrites=false`,
`Elvanto:AllowCreates=false`, `Not in Elvanto:AllowedUpdatePersonIds`, or
`Elvanto:MaxWrites=n already spent`. **A write that actually landed is `PushedToElvanto`, and only
that.** If you see one before you meant to, something got out.

`PushedToElvanto` covers both kinds — a field update, and a create with reason `CreatedNewInElvanto`
(`Apply.cs:485`). **`Created` is not an Elvanto write.** It is a person created *locally* from the
Elvanto roll, reason `NewFromElvanto` (`Apply.cs:113`); the 2026-08-27 run logged two of them with
writes off and nothing left the process. An earlier version of this doc listed `Created` alongside
`PushedToElvanto`, which turns every ordinary inbound create into a false alarm.

Reading Elvanto directly is the other check, and **reads are the only calls you may make by hand**.
`people/getAll`, `people/getInfo` and `services/getAll` are reads. `people/create` and `people/edit`
are mutations and are the engine's alone — see rule 2. The key is in
`GSBC.ImpactKids.Grpc/appsettings.Development.json` under `Elvanto:Authentication` — read it into a
shell variable, never echo it, never put it in a doc or a commit:

```bash
export CFG=GSBC.ImpactKids.Grpc/appsettings.Development.json
AUTH=$(python3 -c "import json,os
print(json.load(open(os.environ['CFG'],encoding='utf-8-sig'))['Elvanto']['Authentication'],end='')")
curl -s -u "$AUTH" -H 'Content-Type: application/json' \
  -X POST 'https://api.elvanto.com/v1/people/getInfo.json' -d '{"id":"<elvanto-id>"}' | python3 -m json.tool
```

---

## Environment

```bash
./db-restore.sh --yes      # newest *.dump in repo root; force-drops, restores, migrates
```

Run the app via the Rider run configuration `GSBC.ImpactKids.AppHost: https`, never `dotnet run`.
App at `https://localhost:7263`. Sign in with `/bff/dev-login?returnUrl=/Sync`.

Driving the sync without the UI, when the browser is unavailable — grpc-web, cookie from dev-login.
`SyncWithElvantoRequest` is now empty, so the body is a zero-length message: a 5-byte grpc-web frame
with a length of 0 and no payload. The old `\x10\x02` was `Mode=DryRun`, a field that no longer exists.

```bash
curl -sk -c jar -b jar -L -o /dev/null "https://localhost:7263/bff/dev-login?returnUrl=/Sync"
printf '\x00\x00\x00\x00\x00' > req.bin
curl -sk -b jar -X POST "https://localhost:7263/gRPC/GSBC.ImpactKids.Sync/CreateSync" \
  -H 'Content-Type: application/grpc-web+proto' -H 'x-grpc-web: 1' --data-binary @req.bin
```

**Drive the UI as a user** — `computer` clicks and typing, never `.click()` or `form_input`. Blazor
binds on real events; a JS-set value updates the DOM and changes nothing underneath, which has
already produced a confident wrong "it saved".

---

## State of the branch

Branch `feature/bidirectional-elvanto-sync`, **uncommitted working tree** — 40 changed/new files
spanning three separate pieces of work. Commit or stash before starting; they should not go in as one
commit.

1. **The family mapping** — `ElvantoFamilyLinks` table + migration, `SyncFamilyLinks`,
   `ElvantoPersonSyncService.FamilyLinks.cs`, and the changes to `Priming`, `Fields`, `Load`,
   `Apply`, `Creates`, `FieldReconciler`, `FieldComparison`, `SyncWorkingSet`, `FamilyDescriptor`.
2. **"No family" as a first-class state** — `Person.HasFamily` / `SharesFamilyWith` /
   `FamilyNameOf`, six frontend sites, `CreatePersonRequest.FamilyWithPersonId` and the server-side
   family minting in `PersonServices/Create.cs`, plus `Create`/`Update` writing `Guid.Empty` rather
   than a fresh Guid for "no family".
3. **The sync UI** — nested `Plan` / `Executed` / `Diverged` / `Manual Review` tabs, `PlanTable`,
   `JsonViewDialog`. Independent of the other two.

## Open, and deliberately not done

- ~~**Remove `Scope=Person` and `Scope=Family`.**~~ **Done, 2026-08-27.** `ElvantoSyncScope` is
  deleted outright rather than reduced to `All`, along with `PersonId`/`FamilyId` on the request and
  on `DbSyncOperation` (migration `DropSyncModeAndScope`), and
  `GetPersonByIdOrSearchAsync` / `GetPeopleForFamilyAsync`. `GetPersonInfoAsync` stayed, as required
  — its two write-path callers are real — and it now carries the defect note on itself. The one-line
  model fix is still outstanding; see the next item.

  Done in the same pass: **`ElvantoSyncMode` (`Full` / `AppOnly` / `DryRun`) removed.** Every run
  decides and stops; `ExecutePlan` applies. AppOnly's behaviour is reproduced by
  `Elvanto:AllowWrites=false` on an Execute.

- **The broken `getInfo` also disables the write path's family read-back.** Separate from the
  scopes, and not fixed either. `GetPersonInfoAsync` has two callers outside them, both on the
  `family_id: "new"` path where Elvanto mints a household:

  | Caller | Why it reads back | Effect of the null |
  |---|---|---|
  | `CreatePerson.cs:143` | only when `people/create`'s response omits `family_id` | falls through with `newFamilyId = null` |
  | `UpdatePerson.cs:46` | always — `people/edit` never reports the family it made | `UpdateOutcome(true, null)`, every time |

  Both already carry a comment saying what the null costs: without the id "every later sibling asks
  for `new` and the family fragments into one household per child". For `people/edit` the read-back
  is the *only* source of that id, so that path cannot currently learn the family at all, and no
  `ElvantoFamilyLinks` row can be written for it.

  **The create was run on 2026-08-27 and the answer is that this is not what bites first.** See the
  next item.

  The fix, whenever it is wanted, is one line: make `ElvantoGetPersonInfoResponse.Person` a
  **list** rather than a single object, and take the first element.

- **A create for a person with no local family does not record the household at create time — but
  the next sync picks it up.** Measured end to end on the first real create, 2026-08-27. This is a
  *different* mechanism from the broken `getInfo`, and it fires first; but it is self-healing, and
  an earlier draft of this section wrongly called the household orphaned. It is not.

  The create for app person `01a041b9-…` (`Zzzcreatetest Newperson`, no local family, so
  `FamilyId = Guid.Empty`) went out with `family_id: "new"`. It succeeded: Elvanto minted person
  `0d30dc40-…` **and household `4905`**, confirmed by reading the record back. Locally:

  | | |
  |---|---|
  | `People.ElvantoId` | set — the person is linked |
  | `People.FamilyId` | still `00000000-…` — unchanged |
  | `ElvantoFamilyLinks` rows with `Source=CreatedInElvanto` | **0** |
  | `ElvantoFamilyLinks` total | 511, unmoved |

  The reason is `SyncFamilyLinks.Record`, which refuses through
  `IsMappable(localFamilyId)` — `Guid.Empty` and the ungrouped bucket are not mappable
  (`SyncFamilyLinks.cs:52-53`, guard at line 90). `Apply.cs:502` only calls `LinkFamily` at all when
  `result.FamilyId is not null`, but **even a perfectly returned household id could not have been
  stored**, because the local side of the pairing is `Guid.Empty`. The two failure modes are
  therefore indistinguishable from the database, and it does not matter which occurred: the outcome
  is the same.

  **The next full sync repairs it, through the ordinary inbound path.** The person is now linked, so
  the roll carries their household, and `FamilyId` comes back as a normal inbound field:

  ```
  Kind=InboundField  FieldName=FamilyId  Reason=FirstSync:ElvantoPrecedence
  ObservedAppValue=00000000-…   ProposedValue=fa1643d6-…
  ```

  Executing that gave the person local family `fa1643d6-…` and wrote the pairing
  `fa1643d6-… ↔ 4905` with **`Source=Observed`** (links 511 → 512). So the household is recorded
  after all — just one run later, and by a different route than `Source=CreatedInElvanto`.

  Two things follow, and both are the mild version rather than the alarming one:

  - The create-time link is a **latency** issue, not a loss. Within a single apply, siblings created
    in the same run cannot join each other's new household, because the in-memory link needed for
    that is never recorded — each gets their own. Across runs it settles.
  - **`Source=CreatedInElvanto` may never appear for a no-family create**, so do not use its absence
    as evidence that a create failed. The cleanup query in "Cleaning up afterwards" will not find
    these; look for `Source=Observed` rows pointing at a household you recognise instead.

  Note also that a person created by hand in Elvanto starts with **no household at all**
  (`family_id: ""`), which is why `Zzztestperson Writetest` never produced an inbound `FamilyId` row
  and still reads as "no family" locally. That is Elvanto's behaviour, not a sync fault.
- **The 400-person bucket after execution.** 12 of the 412 are placed by following Elvanto and 3 are
  archived; the remaining ~397 are blank at source and become "no family". Whether they should be
  grouped into real families is a question for someone who knows them.
- **Reports, roster and analytics have not been checked** against people with no family. Explicitly
  descoped by Asher on 2026-08-26.
- **The plan-tab empty state is misleading.** With a search active, a sub-tab with no matches says
  "Nothing to write to Elvanto" rather than "no matches here — 1 in FROM ELVANTO", which has already
  led to a wrong conclusion that a person was absent from the plan.
