# Elvanto sync — handover for Full-run testing

Branch: `feature/bidirectional-elvanto-sync`. Everything below was established by running it
against a restored production dump, not by reading the code.

**Writes to Elvanto have never happened. Not once, in any run.** Keep it that way until the
checks in "Before turning writes on" pass.

---

## Where things stand

Dry runs are in good shape. A clean run against 1772 people / 1726 linked produces:

| | |
|---|---|
| Processed | 1719 |
| Fields from Elvanto | 42 |
| Fields to Elvanto | 40 |
| People to Elvanto (would create) | 29 |
| Manual review | 17 |
| Archived | 7 |
| Conflicts | 0 |

Those numbers are the current baseline. A run that differs sharply from them is worth
understanding before going further.

### Verified working

- **Every outbound field.** Each was edited through the UI and confirmed to produce an outbound
  entry: first name, last name, email, phone, date of birth, first time, media consent, and the
  medical/allergy text. School grade correctly produces none (Elvanto owns those IDs).
- **Dates** convert AEST↔UTC correctly — a person edited to 15/10 stores as 14/10 14:00Z and
  pushes back as `2016-10-15`.
- **Medical notes and allergies**, which previously never pushed at all. Allergen and condition
  *names* now travel, not just free-text notes, and severity comes through as `(SEVERE)`.
- **First-sync merge**: Elvanto text the app does not already say is carried across verbatim, so
  "Eggs & Milk & Nuts" cannot be replaced by "Allergies: Eggs" and lose the rest. Elvanto text
  dropped is down from 28 cases to 1, and that one is correct (Elvanto says "No known allergies"
  where the app knows about a real pollen allergy).
- **Change tracking** on allergies and medical notes, including deletes — these live in their own
  tables and were invisible to the interceptor before.
- **Manual review queue**: potential duplicates are queued, actionable, and durable.
  Approving keeps the create suppressed; denying releases it. Both survive later runs.
- **Partial-fetch protection** (see below).

### Never exercised

1. **A Full run.** Zero exist: `SELECT count(*) FROM "SyncOperations" WHERE "Mode"='Full'` = 0.
2. **The last-write-wins path.** Dry runs roll back, so `ElvantoFieldSnapshots` and
   `SyncMetadata` are both still empty. Every run so far has therefore been a *first* sync.
   Nothing has ever taken the "both sides changed, newest wins" branch.
3. **Any real write to Elvanto** — no `people/create` or `people/edit` call has been attempted.
4. **Creating a person in Elvanto**, and the ID coming back and being stored.

---

## The write kill switch

Two independent locks, both on by default.

`Elvanto:AllowWrites` is a plain `bool` on `ElvantoConfig` with no initializer, so absent
configuration binds to `false`. No `appsettings*.json` mentions it.

1. **The choke point.** Every Elvanto call goes through `ElvantoService.SendMessage`. Request
   types declare `static abstract bool IsMutation`, and a mutation is refused above the line
   that touches `HttpClient`, with the payload logged. This is per-transport, not per-caller: new
   push code added later still cannot get out.
2. **Each call site.** `CreatePersonAsync` and `UpdatePersonAsync` check `WritesEnabled` and
   return early, logging the exact JSON they would have sent.

Only two endpoints mutate: `people/create.json` and `people/edit.json`. The three read endpoints
are unaffected and do hit Elvanto for real.

To confirm nothing leaked after any run:

```bash
# in the grpc console log
grep -c 'ELVANTO WRITE BLOCKED\|SUPPRESSED' ...    # expect > 0 on a Full run with writes off
```

---

## The dangerous bug that was found, and its guards

A dry run archived **726 children**. Archiving sets `DeletedAtUtc`, and six of the seven tables
that reference a person are `ON DELETE CASCADE`.

Cause: the Elvanto paging loop gave up on a failed page and returned what it had. Nothing
downstream could tell a truncated roll from a complete one, so every linked person missing from
the partial list looked deleted. The only guard caught a fetch of exactly zero; 1000 of 1719
sailed through.

Two guards now:

1. `RetrieveElvantoPeople` retries a page three times then **throws** `ElvantoFetchException`.
   It also holds Elvanto to its own reported total. There is no partial-success outcome.
2. A full-scope sync aborts if the roll covers under `MinimumElvantoCoverage` (90%) of the linked
   people — *before* the archive step, recording `Status=Failed` with the reason.

**The truncation was intermittent**: same code, same data, one run got 1719 and the next got
1000. The retry should absorb it but has only been seen to succeed, never observed recovering
from the failure it exists for. Watch `Processed=` on every run. Anything other than ~1719 with
this dataset deserves a stop.

---

## Testing the Full run

Suggested order. Do not skip to step 3.

### 1. Full run with writes still off

Purpose: lay down `SyncMetadata` and `ElvantoFieldSnapshots`, which no run has ever committed,
and confirm a Full run commits cleanly.

Expect: same counts as the dry-run baseline, `Archived=7`, and afterwards non-zero snapshots and
metadata. Audit rows should say `WouldPushToElvanto` (not `PushedToElvanto`) because writes are
off — if any row says `Pushed`, stop, that means a write was attempted.

Suppressed creates deliberately leave `ElvantoId` unset. A create cannot be faked: there is no
ID to store, and inventing one would link a child to nothing and poison the real sync later. So
`WouldCreateInElvanto` stays at 29 rather than becoming real links.

### 2. Second Full run — the untested path

Purpose: with snapshots now present, this is the first run that is *not* a first sync.

Expect near-silence: the same people should not re-push. Any field that pushes on every run is
churn and a bug — that is exactly how the phone-number formatting problem was found (five people
re-syncing forever because the app renders `0435 862 120` and Elvanto stores `0435862120`).

Then, to reach the conflict branch: edit a field in the app **and** the same field in Elvanto,
then run again. Newest edit should win. `Conflicts` has been 0 in every run so far, so this
branch has never executed.

### 3. Only then, writes on

Add `"AllowWrites": true` under the `Elvanto` section, and start with `Scope=Person` on one
volunteer record you do not mind changing — not `Scope=All`.

**Before turning writes on:**
- Confirm the 29 would-creates are people who genuinely should exist in Elvanto. Several look
  like guardians rather than children; if guardians should not be pushed, that is a missing rule
  and 29 new records in production.
- Read the 40 outbound field pushes. First sync overwrites Elvanto for this field set.
- Decide about the 17 duplicates. Denying one creates that person in Elvanto.

---

## Environment

```bash
./db-restore.sh                 # newest *.dump in repo root; --yes to skip the prompt
```

Discovers the container and password from Docker; force-drops, restores, then applies pending
migrations. `*.dump` is gitignored — the dumps are real people's data, keep them out of git and
delete local copies when done.

Run the app via the Rider run configuration `GSBC.ImpactKids.AppHost: https` — never
`dotnet run`. App is at `https://localhost:7263`. Sign in with
`https://localhost:7263/bff/dev-login?returnUrl=/Sync`.

Migrations on this branch: `20260824165215_AddSyncTables`,
`20260825041947_SyncFieldConfigCorrections`, `20260825045004_AllowMultipleReviewsPerPerson`.

### Traps that have already cost time

- **Drive the UI as a user.** Never `.click()`, never `form_input`. A JS-set value shows on
  screen and never reaches Blazor — this produced a "saved" edit that never saved and a
  change-tracking interceptor wrongly declared broken. Clicks via `computer left_click`, text via
  `computer type`. MudSelect dropdowns do not open on click; focus them and press Enter.
- **Verify the DLL timestamp before `dotnet ef migrations add`.** `build_solution` can report
  success without recompiling, and EF then reads a stale assembly and writes an empty migration.
  Rebuild with `{rebuild: true}`.
- **The console log is tail-truncated.** Payload lines from a full-scope run scroll off. Read the
  audit tables instead; they are authoritative and are what the UI shows.
- **The Aspire dashboard MCP only connects if the app was already running when the session
  started.** Not a config fault, and editing `.mcp.json` will not help mid-session. Its endpoint
  is plain `http` on 16036 — probing `https` returns `000` and looks like a dead dashboard.
- **`GsbcDbContextFactory` hardcodes port 60536** but a persistent container keeps the port it
  was created with (currently 61645). Direct `dotnet ef` commands that need the database will
  fail; `db-restore.sh` passes the discovered port explicitly.

### State the database is currently in

`people=1772 linked=1726 reviews=18 snapshots=0 metadata=0 fullruns=0`

Test edits left behind deliberately, so the change-tracking path has something to push:

- **Abigail Escuyos** (`019a1025-1d34-72e9-a4bc-6bad2a2ac90d`) — first name, last name, DOB,
  first time, media consent, school grade all altered; allergy "Nuts" marked severe with a note.
- **Jocelyn Lukey** (`019a58df-dbe7-7a55-aba1-a5144ec3cdd9`) — email and phone altered.
- **Isabel Roberts** — duplicate review Approved. **Sophie Lawless** — Denied.

Re-running `db-restore.sh` wipes all of that and returns to a clean first-sync state.

---

## Open questions, none blocking

- **Guardians among the 29 would-creates.** Needs a decision before writes go on.
- **Confidence on duplicate reviews reads 50%**, which is a hard-coded placeholder, not a
  computed score. Showing which person the name collides with would be more use.
- **Merge feature** is agreed for later: survivor is the linked person, attendance collisions
  merge into one visit, and "Same person" queues a merge with a preview rather than merging
  immediately. The sync side is already ready for it — approved duplicate rows are durable and
  are the work-list — but the row records the loser's `PersonId` and the winner's `ElvantoId`, so
  the survivor is resolved by lookup. Storing the survivor's `PersonId` explicitly is a column
  and a migration, cheapest now while nothing depends on the shape.
- A false **"Somebody has made modifications, your edit has been cancelled"** toast appears on
  saves that do succeed. Not sync-related.
