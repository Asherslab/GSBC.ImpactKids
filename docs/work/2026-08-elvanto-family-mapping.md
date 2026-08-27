---
title: Elvanto sync — a persisted local-family ⟷ Elvanto-family mapping
kind: handover
status: built
module: sync
opened: 2026-08-26
verified: 2026-08-26
code:
  - GSBC.ImpactKids.Grpc/Features/People/Sync
  - GSBC.ImpactKids.Grpc/Features/Elvanto/ElvantoServices
  - GSBC.ImpactKids.Grpc/Data/Models/Sync
---

> **Superseded in part, 2026-08-27.** `ElvantoSyncMode` (`Full` / `AppOnly` / `DryRun`) and
> `ElvantoSyncScope` (`All` / `Person` / `Family`) have since been removed from the codebase. Every
> run now decides a plan and stops, over the whole roll; `ExecutePlan` applies it. Where this document
> reasons about modes or scopes, read it as a record of what was true at the time — the reasoning
> stands, the surface it describes is gone. Current behaviour: [Elvanto Bidirectional
> Sync](../sync-feature.md).


# Elvanto sync — a persisted local-family ⟷ Elvanto-family mapping

For whoever builds this next. Every number below came from real DryRuns against the church's live Elvanto
account on 2026-08-26 and from `curl` against the Elvanto API, not from reading code. Read
[2026-08-elvanto-sync-refactor.md](2026-08-elvanto-sync-refactor.md) first if you have not: this builds on the
plan/apply split it describes.

**Do not enable writes to do any of this.** `Elvanto:AllowWrites`, `AllowCreates` and `AllowUpdates` stay
`false`. Everything here is verifiable from DryRun, which reads Elvanto and writes only local plan and audit
rows.

---

## The problem, in one sentence

A local family is a bare `uuid` on `People` with nothing that records which Elvanto household it *is*, so the
run re-derives that fact every time from whoever else happens to be in the fetched roll — and for 494 people
it cannot, forever.

### What a run reports today

A full DryRun over 1719 Elvanto people and 1726 linked app people produces 494 `Diverged` audit rows, every
one of them `FamilyId`. They are two unrelated problems that used to share one reason string:

| Reason | Count | What it actually means |
|---|---|---|
| `ElvantoFamilyBlank` | 397 | Elvanto holds no household for this person |
| `ElvantoFamilyUnmapped:<id>` | 97 | Elvanto names a household this app cannot place |

That split is new — the diagnostics that produce it are in the working tree, see "State of the branch" below.
Before them, all 494 read `ElvantoValueUnknown`, which is why this went unexplained for so long.

### The 397 are not a bug

Confirmed against the API and against the Elvanto dashboard. Every one of the 397 has `family_id: ""` **and**
`family_relationship: "Other"`, consistently from both `people/getAll` and `people/getInfo`:

```json
{ "firstname": "Nathanael", "lastname": "Mannah", "family_id": "", "family_relationship": "Other",
  "contact": 0, "archived": 0, "status": "Active" }
```

Elvanto's UI lists that person as **"No Family"** (confirmed by Asher, 2026-08-26). Across the whole roll, all
397 blanks carry `family_relationship: "Other"`, against Child (644) / Primary Contact (468) / Spouse (192) for
people who do have a household. So a blank `family_id` is **known to be nothing**, not unknown — and the
engine currently treats it as unknown and reports it as a finding on every run.

### The 97 are the real bug, and this is why

Lucas Mayers (`efa81357-7e61-4abf-b9dc-b1e4d44a5433`) has a family in Elvanto, and both endpoints say so:

```json
{ "firstname": "Lucas", "lastname": "Mayers", "family_id": "42", "family_relationship": "Child" }
```

Family 42 has four members. Fetch the roll the way the sync does — `contact: "no"` at
`GetPeople.cs:243` — and only one of them arrives:

| Member | Relationship | `contact` |
|---|---|---|
| Steve Mayers | Primary Contact | **1 — excluded from the roll** |
| Hannah Mayers | Child | **1 — excluded from the roll** |
| Jonathan Mayers | Child | **1 — excluded from the roll** |
| Lucas Mayers | Child | 0 — the only one the run sees |

`TranslateFamily` (`ElvantoPersonSyncService.Priming.cs:98`) answers "which local family is Elvanto household
42?" only from *other* app people already known to be in it, and the membership it consults
(`ElvantoPersonSyncService.Load.cs:69`) is built purely from the fetched roll. One member in the roll means no
evidence, which means `CannotRead("ElvantoFamilyUnmapped:42")` at `Priming.cs:118` and a divergence — every
run, forever. Dropping the contact filter would take the roll from 1719 to 2976 people, 1257 of them contacts.

**The exclusion of the asking person is deliberate and must stay.** It is what stopped fourteen people
ping-ponging between families; the reasoning is in the doc comments on `TranslateFamily` and on
`SyncWorkingSet.ResolveFamilyInElvanto`. This design does not weaken it — it removes the need for the
inference altogether.

---

## The decision

**Keep the local family GUID. Add a persisted table mapping it to the Elvanto family id.**

### Rejected: use Elvanto's number as the local family id

Considered and rejected on 2026-08-26. It looks simpler and is a trap:

- **A family Elvanto has not created yet has no number.** Elvanto mints `family_id` when a person is created
  there, so a family grouped locally on a Sunday morning would need a sentinel until the next sync — two id
  spaces again, with extra steps.
- **The numbers are per-tenant.** Restore production into dev pointed at a sandbox Elvanto and every family id
  silently means a different household. GUIDs are environment-independent, which is what made the restored-dump
  backlog recoverable at all.
- **Elvanto owns the number's lifetime.** Merging two households there retires an id; `People` rows would point
  at nothing, with no record of what they meant.
- **It fixes none of the 397**, which are blank at source and would simply become null.

### Why a table and not another inference

The mapping is the only thing that can break the bootstrap. Today "Elvanto household 42 is unknown here" is a
dead end. With a stored row it becomes an action: mint one local family for 42, record the pair, done — and
Lucas is placed without the run ever seeing Steve, Hannah or Jonathan. **This is not the minting that caused
the incident.** That was per person, per run, with no memory, so it re-fired forever and scattered 411 people
into one-person households. Keyed on the Elvanto family id and persisted, every member of household 42 resolves
to the same local family, this run and every run after.

It also makes the outbound direction honest. `Apply.cs:363-364` and `Apply.cs:480-481` already learn the id
Elvanto returns when a family is created — and throw it away when the run ends.

---

## What to build

### 1. The table

```
ElvantoFamilyLinks
  Id               uuid  PK
  LocalFamilyId    uuid  unique
  ElvantoFamilyId  text  unique
  LinkedAtUtc      timestamptz
  Source           text   -- Seeded | Observed | CreatedInElvanto
```

Both sides unique: one local family is one Elvanto household. `Source` is worth having the first time two rows
disagree and someone has to work out where each came from.

There is no `Families` table in this app and there never has been — a family is just a shared `uuid` column on
`People` (`FamilyId uuid NOT NULL DEFAULT '00000000-…'`, no FK, no entity). So this table is additive and
nothing else needs migrating.

### 2. Seed it from what the app already knows

> **Corrected while building, 2026-08-26.** The paragraph below named the wrong column, and following it
> literally is expensive — see "What was actually built".

~~`ElvantoFieldSnapshots` where `FieldName = 'FamilyId'` holds the Elvanto family id per person in
`LastSeenValue` — 1225 rows after a full run.~~ It does not. This branch compares family in the **app's**
terms, so a `FamilyId` snapshot's `LastSeenValue` is the local Guid; the Elvanto side of the pair is not in
that table at all. Seed from the **fetched roll** instead: for every Elvanto person with a non-blank
`family_id` who is linked to an app person, the pair is `(app.FamilyId, elv.family_id)`.

**Do not seed the bucket.** `b1680e5d-01cc-4472-9e71-5df136814247` is the "no family yet" bucket and holds 412
unrelated people (Knobel, Khamsong, Davis, Yansom…). Seeding a pair on it would declare 412 strangers one
household and start syncing them as one. Exclude that GUID explicitly, in code, with a comment saying why —
and see "Out of scope" below.

Any local family that resolves to more than one Elvanto id, or vice versa, is not seeded: write it out for a
human instead.

### 3. Use the table instead of the inference

- `TranslateFamily` (`Priming.cs:98`) reads the table. A hit is a known value.
- A miss with a **non-blank** Elvanto family id is now *known-but-new*: mint one local family GUID for that
  Elvanto id, insert the row, and treat it as the answer. Deterministic per Elvanto family id — never per
  person, never per run.
- `Load.cs:59-76` stops deriving `FamilyIdMap` / `FamilyMembership` from the roll; the table is the source.
  Keep `ResolveFamilyInElvanto`'s asker-exclusion semantics for anything that still needs them.
- `Apply.cs:363-364` and `Apply.cs:480-481` persist the returned id as a row with `Source = CreatedInElvanto`
  rather than only recording it in memory.
- `Creates.cs:71` (`knownFamily`, which decides between a real family id and `ElvantoService.NewFamily`) reads
  the table.

### 4. Treat a blank `family_id` as "no household", not as unknown

> **Superseded, 2026-08-26.** Asher's call: record it as an actual empty family (`Guid.Empty`) rather than
> settling it silently. See "A blank `family_id` is now 'no family'". The paragraph below is why it was
> originally scoped as do-nothing.


`Priming.cs:65-67` currently returns `CannotRead("ElvantoFamilyBlank")`. Elvanto is stating a fact: this person
is in no household. The app must **not** clear or change the local family on the strength of it — the local
grouping may be real and Elvanto simply does not have it — but it must stop reporting it as a finding. Settle
it as agreed-with-reason or skip it; either way those 397 rows leave the Diverged tab.

Keep `FamilyIdDescriptor.SetOnApp`'s refusal to parse a non-Guid (`Descriptors/FamilyDescriptor.cs`). It is
what stops a value it cannot read from minting a household.

---

## Verifying it

Run a full DryRun from `/Sync` (Mode `DryRun`, Scope `All`) and compare against the numbers at the top of this
doc. Expected afterwards:

| Group | Before | After (measured, 2026-08-26) |
|---|---|---|
| `ElvantoFamilyBlank` (397) | diverges every run | not reported at all — settled as `Skipped` |
| `ElvantoFamilyUnmapped` (97) | diverges every run | paired on first sight, one table row each |
| The 1225 already working | fine | fine, and no longer re-derived per run |
| The 412 bucket | wrong and invisible | 12 placed by following Elvanto, 400 left for a human |

Read the result from the database rather than the logs — the gRPC log buffer is saturated by EF command logging
and `Fanning Out Heartbeat`, and the useful lines are evicted within seconds:

```bash
c=$(docker ps --format '{{.Names}}' | grep '^sql-')
docker exec -e PGPASSWORD="$(docker exec $c printenv POSTGRES_PASSWORD)" $c psql -U postgres -d impact-kids -c \
  'select "Reason", count(*) from "SyncAuditLogs"
   where "SyncOperationId" = (select "Id" from "SyncOperations" order by "StartedAt" desc limit 1)
     and "EventType" = '"'"'Diverged'"'"' group by 1 order by 2 desc;'
```

The sync page itself is the other check: `Plan` / `Executed` / `Diverged` / `Manual Review` at the top level,
and the Diverged tab should be near-empty afterwards.

### Reading the Elvanto API directly

Read-only calls are the fastest way to settle "what does Elvanto actually say". The API key lives in
`GSBC.ImpactKids.Grpc/appsettings.Development.json` under `Elvanto:Authentication` — read it into a shell
variable, never echo it, and never put it in a doc or a commit:

```bash
export CFG=GSBC.ImpactKids.Grpc/appsettings.Development.json
AUTH=$(python3 -c "import json,os
print(json.load(open(os.environ['CFG'],encoding='utf-8-sig'))['Elvanto']['Authentication'],end='')")
curl -s -u "$AUTH" -H 'Content-Type: application/json' \
  -X POST 'https://api.elvanto.com/v1/people/getInfo.json' \
  -d '{"id":"efa81357-7e61-4abf-b9dc-b1e4d44a5433"}' | python3 -m json.tool
```

`people/getAll` takes `{"suspended":"no","contact":"no","archived":"no","page":1,"page_size":1000,"fields":[…]}`.
Drop `contact` to see household members the sync never receives.

---

## Traps

**Never ask Elvanto for the `family` field.** The [People Fields](https://www.elvanto.com/api/people-fields/)
page advertises an optional `family` field — "can only be used when retrieving a person. It will return the
person's family members". It does not work, in two different ways, both verified on 2026-08-26:

- On `people/getAll` it is **silently ignored**. No family node comes back for anybody.
- On `people/getInfo` it **breaks the call**. The request returns nothing usable, the person-scoped sync
  processes zero people, and the operation still completes as `Success` with an empty plan — a silent no-op
  that looks like "nothing to do". Removing the field from the identical request made the same sync write ten
  field snapshots.

That avenue was tried and reverted on this branch. A person object carries `family_id` and
`family_relationship` and nothing else about the household.

**The stale-assembly trap will bite you here.** `mcp__rider__build_solution` can return
`{isSuccess: true, problems: []}` without recompiling the project you just edited, and you will then measure a
run against old code and draw a confident wrong conclusion — this happened twice while investigating. Check the
literal actually made it into the binary before trusting a result (note .NET stores string literals as UTF-16,
so plain `strings` will not find them):

```bash
export DLL=GSBC.ImpactKids.Grpc/bin/Debug/net10.0/GSBC.ImpactKids.Grpc.dll
python3 -c "import os
print(open(os.environ['DLL'],'rb').read().count('YourNewLiteral'.encode('utf-16-le')))"
```

A WASM change needs a full stop-and-restart, not just a rebuild — `.claude/skills/run-and-inspect-app` has the
`pkill` incantation and the reason.

**Do not add contacts to the main roll.** Absence from that roll is what drives archiving — a short roll once
archived 726 children, which is why `LoadWorkingSetAsync` has two independent refusals. If a future change
needs contacts for household membership, it must be a separate lookup-only fetch that never feeds archive or
create decisions. With this design you should not need one at all.

---

## Out of scope, but you will trip over them

- **The 412-person bucket.** `b1680e5d-01cc-4472-9e71-5df136814247` holds 412 unrelated non-deleted people. It
  is a data problem that predates all of this. Nothing may ever *pair* it with a household — but see "Never
  pair the bucket ≠ never let anyone leave it": 12 of the 412 are placed by following Elvanto, and the
  remaining 400 are blank at source and need a human who knows the families.
- **Person-scoped runs skip `FamilyId` entirely.** On 2026-08-26 a Person-scoped DryRun of one linked person
  settled ten fields and produced **no** `FamilyId` row of any kind — no snapshot, no audit row, no plan row —
  while an All-scoped run over the same person diverges on `FamilyId`. Unexplained. The only "nothing" branch
  in `PlanFieldsAsync` is `Skipped`, which should require a `Disabled` direction. Worth settling before anyone
  relies on person- or family-scoped syncs to touch family.

---

## What was actually built, 2026-08-26

Implemented on this branch, verified by two consecutive full DryRuns against the live Elvanto account with
`Elvanto:AllowWrites`, `AllowCreates` and `AllowUpdates` all still `false`.

### Measured

| | Before | After |
|---|---|---|
| `Diverged` rows on `FamilyId` | **494** | **0** |
| `ElvantoFamilyLinks` rows | — | **511** (506 `Seeded`, 5 `Observed`) |
| Planned `FamilyId` changes | — | **411** |
| Outbound family pushes | — | **0** |
| Bucket ever recorded as a `LocalFamilyId` | — | **0** |

Two consecutive runs produced identical numbers — 564 plan rows, 411 of them family, 0 divergences — and the
link table did not grow between them.

The 411 family moves account for the entire backlog, and dissolve the bucket completely:

| | Count | |
|---|---|---|
| bucket → **no family** (`Guid.Empty`) | 397 | Elvanto says "No Family" and the app now records that |
| bucket → a real household | 12 | grouped by household: three Grays, two McDowells, Brice+Sing+Yeo, Curtis+Fox, Axontoniie+Jurekic |
| bucket → archived | 3 | gone from Elvanto (incl. the "Elvanto Support" test record) |
| a real local family → a household | 2 | Nathan and Joshua Holowaty, the two splits, followed |

**Nobody is pulled out of a real shared local family.** Every one of the 397 was in the bucket, which was
never a family. The bucket goes 412 → 0.

### A blank `family_id` is now "no family", not "unknown"

Reversed on Asher's instruction, 2026-08-26, and the earlier refusal above is superseded. Elvanto's UI shows
these people as "No Family" and that is authoritative for this church, so the app records `Guid.Empty`.

**This is not the read that caused the incident, and the difference is exactly one line.** The 411 incident
passed a blank through as null, and `FamilyIdDescriptor.SetOnApp` then fell back to `Guid.NewGuid()` — giving
411 people a brand-new one-person household *each*, re-minted on every run because the base never advanced.
That fallback is gone; `SetOnApp` only assigns a Guid it was actually handed. `Guid.Empty` is one shared
value meaning "none", not 397 fresh families, and it settles a base so it happens once.

Two guards make it safe in the other direction, and both are verified at 0:

- **`Guid.Empty` must never be pushed as a household.** `BuildComparison` sends null outbound for it and
  `ApplyOutbound` refuses an empty value rather than defaulting to `ElvantoService.NewFamily` — the old
  default would have read "this person has no family" as "create a household for them", 400 times.
- **`Guid.Empty` must never enter `ElvantoFamilyLinks`.** `SyncFamilyLinks.IsMappable` already excluded it.

`Translated.HoldsNothing`, `FieldComparison.ElvantoHoldsNoValue` and the reconciler branch that read them are
deleted — the state they represented no longer exists.

**The frontend was updated for this**, and verified by temporarily moving the bucket to `Guid.Empty`,
looking at the pages, and reverting. `Guid.Empty` had to become a first-class "no household" rather than a
family id everything happened to group on — the bucket showed a woman her family as **"Kent (412)"** with 411
strangers listed under "Family Members", and `Guid.Empty` would have done exactly the same.

The rule now lives in one place, on `Person`: `HasFamily`, `SharesFamilyWith(other)` and
`FamilyNameOf(person, people)`. The last one also fixes a latent crash — all three call sites did
`MaxBy(...)!.Key`, and `MaxBy` returns null on an empty sequence.

| Site | Was | Now |
|---|---|---|
| `PersonDetails.razor.cs:77` | `GroupBy(x => x.FamilyId)` offered "no family" as a family | excluded from the picker |
| `PersonDetails.razor` | bound value with no item rendered the raw `00000000-…` | an explicit **"No family"** item |
| `PersonOverview.razor:61` | 411 unrelated "Family Members" | empty |
| `Attendance/Family.razor:41` | 411 unrelated members | empty |
| `Attendance/Family.razor` | "Create Person in Family" passed `Guid.Empty` | hidden when there is no family |
| `Attendance/Family.razor.cs`, `SignIn.razor.cs`, `SignOut.razor.cs` | breadcrumb read "Kent" | the person's own surname |

Verified in the browser: a no-family person shows **Family: No family** and no family members, on the person
page, the attendance family page and the sign-in wizard; a person in a real family still shows
**"Abante (4)"** and their four relatives.

`PersonServices/Create.cs` and `Update.cs` both turned a null incoming `FamilyId` into `Guid.NewGuid()` — a
private household of one, the *old* way of saying "none". Both now write `Guid.Empty`, so the column holds one
answer to that question instead of two. Note the behaviour change: creating a person with no family selected
leaves them with no family rather than silently inventing one, and clearing someone's family now clears it.

### Three places the plan was changed, and why

**The seed reads the roll, not the snapshots.** See §2 above. Following the doc literally produced 409 rows
whose two columns were the *same Guid*, after which every real household looked unknown: 411 freshly minted
one-person households and **1213 planned inbound family moves** — the incident, reproduced exactly. Caught in
a dry run; the rows and that operation were deleted. Check `ElvantoFamilyLinks` for a row whose
`ElvantoFamilyId` parses as a Guid before believing any future seed.

**A miss binds the asker's own local family rather than minting a new one.** The doc says to mint a fresh
Guid per unknown household. That is deterministic, but it also *moves* the asking person out of the family
they are in — 97 people into new one-person households on the first run. Binding their existing family
instead makes it an agreement and moves nobody. It is safe for the same reason the seed is: the asker is
their own evidence exactly once, and from the next question onwards the stored row answers, so a later local
move is a difference the run can see rather than one that confirms itself. Minting is still what happens when
the person has no local family yet, when theirs already is a different household — which is what a move in
Elvanto looks like — or when they are in the bucket.

**A split is followed; only a merge is refused.** These are not the same question, and treating both as
findings left six rows nobody needed to look at. One local family spread across several Elvanto households is
a *split*, and Elvanto owns household structure, so it is simply followed — the household with the most
members keeps the existing local family, the rest get one each, ranked so the answer does not change between
runs. Two local families claiming one household is a *merge*: it would declare people related who are not
currently recorded as such, so it stays `ElvantoFamilyContested:<id>`. There are none in the current data.

### Never pair the bucket ≠ never let anyone leave it

Only the first rule was wanted, and conflating them was costing twelve people their real families.

`b1680e5d-01cc-4472-9e71-5df136814247` is not a household — it is 412 unrelated people sharing one Guid — so
it may never be one side of a link row, and `SyncFamilyLinks.IsMappable` is what guarantees that. But a
bucketed person Elvanto *has* put in a real household should be placed in it rather than left in the pile.
They fall through to the mint, which writes the row on the first member of that household so the rest join
them: the five new families above, from twelve people, not twelve families from twelve people.

This shrinks the bucket from 412 to 400 and needs no human. The remaining 400 are blank at source — Elvanto
says "No Family" for them too — so there is nothing to follow and they still need someone who knows the
families. That is the list to hand Asher, and it is now the *whole* of the bucket problem rather than a
problem mixed in with twelve solvable cases.

### Executed, 2026-08-26

Asher hit Execute on the plan. Applied against a freshly restored production dump:

| | |
|---|---|
| Plan rows applied | **469** |
| Suppressed (Elvanto writes off) | **95** |
| Failed | **0** |
| The bucket | **412 → 3** (the three archived, gone from Elvanto) |
| People with no family | **0 → 398** |

Nothing reached Elvanto: every outbound row is audited
`WouldPush:Elvanto:AllowWrites=false:…` (67) or `WouldCreate:Elvanto:AllowCreates=false` (28).

**One regression, found only after executing, and only because real `Guid.Empty` data existed.**
`Attendance/Family.razor` filtered its list on family alone, so a person with no family dropped off
**their own page** — and that page is where they get signed in, so there was no way to sign them in
at all. Fixed by matching the person as well as their household:
`x.Id == person.Id || x.SharesFamilyWith(person)`, which changes nothing for anyone who has a family
because they already match. The heading now reads "Attendance for Nathanael Mannah" rather than
announcing a family of one.

This is worth remembering as a shape: `SharesFamilyWith` is deliberately false for a person with no
family compared against *themselves*, which is right for a "family members" list and wrong for any
list that is really "this person and their household". Check which one you have.

**And a razor trap, immediately after the doc warned about it.** The fix was first written with its
`@* comment *@` between two component attributes, where razor parses it as an attribute name. It
builds clean and throws at render — `does not have a property matching the name '@* … *@'`. Comments
go *above* the tag. The console also kept showing that exception after the fix: the messages were
scrollback from the previous load, and only clearing the buffer and reloading proved it gone.

### "Create Person in Family" for someone with no family

Hiding the button was the wrong fix for the wrong problem. A person with no household is exactly the
person a leader is most likely to be adding a sibling for, and the button is how they do it.

It is back, labelled **"Create Person in New Family"** when there is no family yet, and it carries a
new `CreatePersonRequest.FamilyWithPersonId` — *"put the new person in this person's family"*.

**The family is minted on the server, not the client, and that is the point.** It has to land on
**both** people or neither: a Guid generated in the browser and applied only to the new person leaves
the existing one behind, and applying it to both takes two calls that can half-fail. `Create.cs`
resolves the family before building the person and lets one `SaveChanges` carry both rows.

`FamilyId` still wins when it is set, so an explicit pick in the family selector — a real family, or
"No family" — always beats the page the leader happened to arrive from. The surname prefill now falls
back to that person's when there is no family to take it from.

Walked end to end in the browser against real data: Nathanael Mannah (no family) → button → surname
pre-filled "Mannah", family selector empty → Create → **both** rows carry the same new family Guid,
the page returns as "Attendance for the Mannah Family" with both people and their sign-in buttons,
and the button reverts to plain "Create Person in Family". Test person deleted and Nathanael restored
to `Guid.Empty` afterwards.

### Where the code is

| File | What it does |
|---|---|
| `Data/Models/Sync/DbElvantoFamilyLink.cs` | the row |
| `Data/Models/Sync/Enums/ElvantoFamilyLinkSource.cs` | `Seeded` / `Observed` / `CreatedInElvanto` |
| `Data/Migrations/20260826031723_ElvantoFamilyLinks.cs` | additive `CREATE TABLE`, unique on both sides |
| `Features/People/Sync/Models/SyncFamilyLinks.cs` | one run's view: both lookups, the bucket guard, the unmappable set |
| `Services/ElvantoPersonSyncService.FamilyLinks.cs` | the seed and the ambiguity refusals |
| `Services/ElvantoPersonSyncService.Priming.cs` | `TranslateFamily` reads the table; blank is `HoldsNothing` |
| `Services/FieldReconciler.cs` | `ElvantoHoldsNoValue` → `Skipped`, so the 397 leave the Diverged tab |
| `Services/ElvantoPersonSyncService.Apply.cs` | a household Elvanto mints is persisted, not just held for the run |

`SyncWorkingSet.FamilyIdMap`, `.ElvantoFamilyIdByLocal`, `.FamilyMembership` and `.ResolveFamilyInElvanto` are
gone, along with `FamilyIdDescriptor.ElvantoFamilyIdByLocal`. The asker-exclusion they existed to implement is
not weakened — it is unnecessary, because a stored row does not move when the roll does.

### Still open

- **Person-scoped runs still skip `FamilyId` entirely.** Unchanged and still unexplained; see "Out of scope".
- **The 400-person bucket.** The 12 with a known household are placed by the plan above; the rest are blank
  in Elvanto too and still need a human.
  `select p."FirstName", p."LastName" from "People" p where p."FamilyId" = 'b1680e5d-01cc-4472-9e71-5df136814247'`
  is the list to hand Asher, after the plan is executed.

---

## State of the branch on 2026-08-26

Branch `feature/bidirectional-elvanto-sync`, **uncommitted working tree**. Two unrelated pieces of work are
sitting in it; commit or stash before starting.

Sync engine — the diagnostics this doc's numbers depend on:

| File | Change |
|---|---|
| `Services/ElvantoPersonSyncService.Priming.cs` | `Translated` carries `Detail`; blank vs unmapped named apart |
| `Models/FieldComparison.cs:65` | `ElvantoUnknownDetail` carries it to the reconciler |
| `Services/ElvantoPersonSyncService.Fields.cs` | passes it through `BuildComparison` |
| `Services/FieldReconciler.cs:51` | uses it as the audit reason instead of a bare `ElvantoValueUnknown` |

Sync UI — a separate change, safe to commit on its own:

| File | Change |
|---|---|
| `WASM/Features/Sync/Pages/Individual.razor` | nested tabs: `Plan` / `Executed` / `Diverged` / `Manual Review` |
| `WASM/Features/Sync/Pages/Individual.razor.cs` | plan split by `PlannedChangeKind`; review rows are not executed |
| `WASM/Features/Sync/Components/PlanTable.razor` | new — plan rows, JSON payloads behind an info button |
| `WASM/Features/Sync/Components/JsonViewDialog.razor` (+ `.css`) | new — pretty-printed payload dialog |

Counts live in tab labels rather than `MudTabPanel` badges: the badge is positioned past the label's right edge
and the tab strip clips it, so a count of 50 rendered as "5".
