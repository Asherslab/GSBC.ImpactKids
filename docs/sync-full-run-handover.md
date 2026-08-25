# Elvanto sync — handover

Branch: `feature/bidirectional-elvanto-sync`. Everything below was established by running it against
a restored production dump and against the church's real Elvanto account, not by reading the code.

**Writes now work and have been used.** Nine people were created in Elvanto and a dozen field
changes pushed, all deliberately and under guards. Writes are off again — see "The write guards".

---

## Where things stand

| | |
|---|---|
| Full runs | many, all `Success` |
| Creates to Elvanto | proven (5 test people + 1 real child, since deleted) |
| Updates to Elvanto | proven (7 fields across 4 people) |
| Conflicts / last-write-wins | proven in both directions |
| Family create, move, and "new" family | proven |
| Suppressed-change bug | **open** — see "The one open bug" |

### Verified working, end to end

- **Create.** Payload accepted, Elvanto id returned and stored locally, person linked. Category,
  family, relationship, birthday, first-time and media consent all land.
- **The `"new"` family chain on create.** The first member of a family with no Elvanto presence
  sends `family_id: "new"`; the id that comes back is recorded mid-run so the rest of the family
  joins it. Proven with a four-person family that landed as one household, not four.
- **Update.** Names, email, phone, date of birth, first time, media consent and the medical/allergy
  text all push and are visible in Elvanto.
- **Family move**, including into a family Elvanto has never seen (which creates it).
- **Last-write-wins in both directions**, decided on Elvanto's own `date_modified`.
- **Scoping.** A run can be restricted to named people for creates and updates independently, with
  a hard ceiling on how many writes may leave the process.

---

## The write guards

Four independent layers. The first three sit above the line that touches `HttpClient`, in the single
`ElvantoService.SendMessage` choke point every call passes through — there is exactly one
`httpClient.SendAsync` in the project.

| Setting | Effect |
|---|---|
| `Elvanto:AllowWrites` | master switch; absent binds to `false` |
| `Elvanto:AllowCreates` / `Elvanto:AllowUpdates` | which *kind* of write may leave; both default `false` |
| `Elvanto:MaxWrites` | hard ceiling on mutations for the life of the **process**; null = no ceiling |
| `Elvanto:AllowedCreatePersonIds` / `AllowedUpdatePersonIds` | *which people*; empty = no restriction |

`AllowWrites: true` alone sends nothing — a write must also name its kind. That is deliberate.

The budget is consumed **after** every other check, so a refused write never spends the allowance,
and it never replenishes: restarting the process is the only reset. Note the restart caveat — a
restart re-arms it.

The allow lists gate different things: creates are gated in the sync loop (everyone else is audited
as `WouldCreate:NotInAllowList`), updates are gated before the request is built so an excluded
person is reported as *would* push rather than pushed.

**Current state: everything off.** `AllowWrites`, `AllowCreates`, `AllowUpdates` all `false`,
`MaxWrites: 0`, both allow lists empty, and the app is stopped.

### Running a controlled write test

1. Set the kind you want on, `MaxWrites` to the exact number of expected calls, and the allow list
   to the specific app person ids.
2. **Dry-fire first**: identical config except `MaxWrites: 0`. Nothing can physically leave, and the
   audit records one `CreateAttempted` row per attempt *with the exact request body*. That is how you
   confirm both the number of writes and their contents before any of them happen.
3. Flip `MaxWrites` to the real number and run once.
4. Read the people back out of Elvanto and check the fields actually landed.

Updates batch per person, so budget = number of people, not number of fields.

---

## The one open bug — a change that did not land is treated as settled

**This is the thing to fix next.** It has now appeared three separate times, each with a different
cause and the same shape: *the app's pending change silently becomes invisible, forever.*

The mechanism: `appChanged` is true only while the app's change is **newer** than the field's
snapshot. If the snapshot advances while a change is still pending, that change can never be seen
again. Nothing reports it. The two sides just stay different.

Three ways it has happened:

1. **Writes suppressed.** The app won a conflict, the push was blocked, the snapshot advanced.
   *Fixed* — the snapshot is now held for fields the app won whose push did not land.
2. **The field was skipped entirely.** Never entered the change logic, so the hold never engaged.
   *Not fixed.*
3. **The request was sent but did not carry the field.** Elvanto answered `ok`, so `pushLanded` was
   true, the hold was skipped and the snapshot advanced. *The specific cause is fixed (both outbound
   branches now share one `ApplyOutbound` helper) but the class is not.*

The hold is too narrow: it only covers app-won conflicts, and it trusts the call's return value.
A sounder rule is to advance a field's snapshot only when the request **actually carried that
field** — check the built payload, rather than trusting that the call succeeded.

Recovery, when it does happen: only a fresh app-side edit. Re-saving the same value does not help
(no change is logged). Clearing the field and setting it back does, as long as no sync runs in
between.

---

## Bugs found and fixed this round

Each of these would have hit the first production write.

**`Guid.NewGuid()` on navigation-added children.** New `DbAllergy`/`DbMedicalNote` rows were created
with a key already set. They reach the context through a navigation collection, not a `DbSet`, and
EF reads a set key as "this row exists" — so it issued an `UPDATE` matching nothing and the whole
save died with a concurrency error. Invisible until the first Full run, because **a dry run never
calls `SaveChanges` at all**. Fixed by using `Guid.Empty`, matching the house idiom.

**Elvanto's `id`/`name` never deserialised.** `MediaConsent` and `SchoolGrade` had no
`[JsonPropertyName]` and the response options don't set `PropertyNameCaseInsensitive`. Elvanto sends
lowercase keys, so both properties bound to null on every read — the object itself wasn't null, so it
failed silently and **media consent always read as "Not Requested" whatever Elvanto held**. Inbound
consent changes could never be seen. Fixed with explicit attributes.

**A `select` custom field wants the option id as a plain string.** Elvanto's docs say Drop Down and
Checkbox fields must be arrays; that is wrong for `select` (this account has no checkbox or
multi-select type at all). Rejected: `["Yes"]` and `["<option id>"]`. Accepted: `"<option id>"`.
Option ids come from `people/customFields/getAll`, which is the authority; they are constants in
`MediaConsentOptions`. This affected `people/edit` identically — every media-consent push would have
failed.

**`"Allergies: \nMedical: "` written as real content.** The legacy fallback tested row *count*, and a
person recorded as having no known allergies still has rows (pointing at "None"). Fixed by judging
emptiness on the joined text.

**Failures were unreadable.** A failed create reported no reason, and the console was unreachable.
Elvanto's own error text now goes into the audit trail, distinguishing "refused before sending" from
"Elvanto rejected the payload". This is what turned a blind retry loop into two targeted fixes.

**`FailureReason` was left null** on the exception path, so a failed run showed `Failed` with no
reason. Now recorded, including the conflicting entity for a concurrency failure (type and key only —
property values would carry medical text into a column people read casually).

---

## Family sync — how it works, and the trap in it

Family is **bidirectional** with last-write-wins, decided on `date_modified`. Getting there needed
three fixes, all the same idea: **a family's Elvanto identity is evidenced by its other members,
never by the person being checked.**

- `familyIdMap` (Elvanto → local) is seeded from linked members. A person who is the *sole* member of
  their Elvanto family teaches the map their own pairing — so translating back gave their own value,
  the hashes compared equal, and the field was skipped before any change logic ran. A lone member
  could never disagree with themselves.
- `ResolveFamilyInElvanto(localFamily, askingPerson)` excludes the asker. A lone mover therefore has
  no evidence, which is correct: their new family genuinely has no Elvanto counterpart, so one must
  be created (`family_id: "new"`).
- Comparison for this field happens **in Elvanto's terms**, not by translating back into the app's.
- `family_id` is set by the orchestrator, not the descriptor: a descriptor instance is shared across
  everyone in the run, so it structurally cannot answer a question whose answer depends on who is
  asking.

Asymmetry worth knowing: on a **create**, an unresolvable family sends `"new"`. On an **update** it
also sends `"new"` now — dropping it silently was worse, and it does not scatter households, because
once the push lands Elvanto reports the person in the new family and the next run resolves it from
them. Within a single run, the id that comes back is recorded so a second mover joins rather than
asking for another `"new"`.

### The trap: a DB config row overrides the descriptor

`SyncFieldConfigs` rows override a descriptor's `DefaultDirection` entirely. Making
`FamilyIdDescriptor` `Bidirectional` did nothing while the seeded row still said `InboundOnly` — the
move was dropped **with no audit row at all**. Fixed by migration `20260825101023_FamilyIdBidirectional`.

**Changing a descriptor's direction requires a matching migration, or the change does nothing and
says nothing.**

`SchoolGradeId` and `FamilyGuardian` remain `InboundOnly` in both places. School grade is correct and
intended — Elvanto owns those ids, and a created child therefore arrives in Elvanto without a grade.

---

## Elvanto API notes

- `date_modified` is returned on **every** people response and is **UTC** (verified against a known
  edit). It cannot be requested via `fields` — asking for it by name is rejected as a field that does
  not exist. Empty for a person never edited; fall back to `date_added`.
- It is **per person, not per field**, so for a single field it is an upper bound.
- `people/create` returns both the new person id **and** `family_id`. `people/edit` does **not**
  report the family, so a create-a-family edit reads the person back to learn it.
- `family_id` is documented as an integer but is sent as a string; `"new"` is a valid value for the
  same parameter. Both work.
- Custom fields are `custom_<id>` nested under `fields`. `datepicker` and `textarea` take plain
  strings; `select` takes the option id as a plain string.
- Mobile numbers are normalised by Elvanto on the way in (`0400 000 001` → `0400000001`). This is why
  the phone descriptor strips spacing before comparing.

---

## Environment

```bash
./db-restore.sh                 # newest *.dump in repo root; --yes to skip the prompt
```

Discovers the container and password from Docker; force-drops, restores, then applies pending
migrations. `*.dump` is gitignored — real people's data, keep it out of git and delete local copies.

Run via the Rider run configuration `GSBC.ImpactKids.AppHost: https` — never `dotnet run`. App is at
`https://localhost:7263`. Sign in with `https://localhost:7263/bff/dev-login?returnUrl=/Sync`.

Migrations on this branch: `20260824165215_AddSyncTables`, `20260825041947_SyncFieldConfigCorrections`,
`20260825045004_AllowMultipleReviewsPerPerson`, `20260825101023_FamilyIdBidirectional`.

### Traps that have already cost time

- **Drive the UI as a user.** Never `.click()`, never `form_input`. JS is for *reading* facts only
  (and for scrolling a 525-item dropdown into view — the family and person pickers have no search,
  which makes them unusable for targeting by hand).
- **Verify the DLL timestamp before `dotnet ef migrations add`.** `build_solution` can report success
  without recompiling, and EF then reads a stale assembly and writes an empty migration. Rebuild with
  `{rebuild: true}` and check the timestamp. Migrations live in `Data/Migrations`, not `Migrations`.
- **The console log is unreachable in some sessions.** The Aspire dashboard MCP only connects if the
  app was already running when the session started, and the Rider run log carries no child output.
  This is why failures now record their reason in the audit tables — they are authoritative and are
  what the UI shows.
- **A false "Somebody has made modifications, your edit has been cancelled" toast** appears on saves
  that do succeed. Verify in the database before believing it. Not sync-related.
- **`GsbcDbContextFactory` hardcodes port 60536** but a persistent container keeps the port it was
  created with. `db-restore.sh` passes the discovered port explicitly.
- **`Scope=Person` on an *unlinked* person is dangerous and unfixed.** It fetches the whole Elvanto
  roll so the matcher can run, then the main loop creates a local app person for every row that does
  not match — roughly 1718 spurious people. Local only, no Elvanto writes, but it would wreck the
  database. Use `Scope=All` with the allow lists instead.

---

## State the database is currently in

Restored from the production dump, then: 4 test people created and linked, 1 real child (Amina Tran)
created and since deleted from Elvanto and unlinked locally.

- `test user - 1` … `test user - 4`, all in one family (Elvanto `4903`), all linked. Two guardians,
  two children, with bogus emails/phones/DOBs, media consent, first-time dates, and medical/allergy
  rows including a severe allergy and an unlinked allergen.
- `test user - 5` was created and then deleted, in Elvanto and locally.
- Elvanto families `4904` and `4905` were created during testing and are now empty.
- **Jocelyn Lukey's email change is still pending outbound** and was deliberately excluded from every
  write test by the update allow list. It will push the moment updates are enabled without a list.
- Deliberate edits remain on Abigail Escuyos and others from the earlier round.

`db-restore.sh` wipes all of that and returns to a clean first-sync state. It does **not** clean up
Elvanto — the test people and empty families are still there.

---

## Uncommitted work

Nothing is committed. 19 modified files, 4 new (`ElvantoWriteBudget`, `MediaConsentOptions`, and the
migration pair), ~660 insertions.

## Open questions, none blocking

- **The suppressed-change bug above.** The one to fix next.
- **Merge feature** is agreed for later; the sync side is ready for it.

Closed, no action needed:

- Guardian family relationship being guessed as `Spouse` (grandparent/aunt included) — accepted as-is.
- Created people landing in the Visitor category, diverging from existing siblings — correct behaviour.
- Duplicate-review confidence reading a hard-coded 50% — accepted as-is.
- Family audit rows showing local Guids for a field compared in Elvanto's terms — accepted as-is.
