---
title: Gender and person photos — execution handover
kind: handover
status: accepted
module: people
opened: 2026-08-29
verified: 2026-08-29
code:
  - GSBC.ImpactKids.Grpc/Features/People/Sync/Descriptors
  - GSBC.ImpactKids.Grpc/Features/Elvanto/ElvantoServices
  - GSBC.ImpactKids.WASM/Features/People/Components/Individual
  - Charts/impact-kids/templates
---

# Gender and person photos — execution handover

For whoever picks this up. **[The plan](2026-08-gender-and-person-photos.md) is the specification —
read it end to end before touching anything.** This doc does not restate it. It covers how to
execute it: what order, what runs in parallel, what will bite you, and what you must stop and ask
for.

The branch is **`feature/gender-and-person-photos`**, already created. Commit per slice on it
without asking; never push.

## The five things most likely to waste your day

Read these even if you skip everything else here. Each one has already cost time in this repo.

1. **Never `dotnet run`.** Run configurations through `mcp__rider__execute_run_configuration` only.
   See the `run-and-inspect-app` skill. This includes one-off side experiments.
2. **Run migrations yourself — do not ask.** Every migration in this plan is additive (nullable
   columns, one new table), which [AGENTS.md](../../AGENTS.md) makes free, and `dotnet ef` is the
   one CLI exception to the run-configurations rule. Locally the worst case is `./db-restore.sh`,
   which drops the database, restores a known prod dump and re-migrates. Halting on this wastes a
   whole slice waiting on a human for something that undoes itself in a minute.
3. **Check the built DLL is newer than your edit before generating a migration.**
   `build_solution` can return success without recompiling, and `dotnet ef migrations add` reads the
   assembly, not your source — the result is a migration with an empty `Up()` that looks like "EF
   found no model change". `stat -f '%Sm %N' GSBC.ImpactKids.Grpc/bin/Debug/net10.0/GSBC.ImpactKids.Grpc.dll`.
4. **Never write to Elvanto by hand.** Not by `curl`, not by script, not through their UI, not "just
   to set up a test". The sync engine is the only thing that may ever write — Asher's standing
   instruction, recorded in [the write-testing gate](2026-08-elvanto-write-testing.md). Reads are
   free and encouraged.
5. **Never add `"picture"` to the `Fields` array** in `ElvantoService.FetchPageWithRetries`. Elvanto
   rejects it with `code 250: A field does not exist (picture)` and the *entire* people fetch fails,
   which downstream reads as an empty roll. It is returned by default. See
   [the Elvanto API reference](../modules/elvanto/api-reference.md).

## Definition of done, per slice

From [AGENTS.md](../../AGENTS.md), and the third one is the one people skip:

- builds — `mcp__rider__build_solution`
- registers what it added — `Program.cs` mapping, DI, WASM client registration
- **seen working in the running app.** Start through Rider, sign in at `/bff/dev-login`, drive the
  page, read the rows it actually wrote. This repo's tests cover the sync reconciler and nothing
  else, so for anything with a UI this is the only gate there is.
- updates any `docs/modules/` doc whose behaviour it changed

Drive the app from a **nodeterm browser node**, not Chrome — the `run-and-inspect-app` skill has the
procedure, including why a `@ref` click there actually reaches Blazor and a synthetic one does not.
Chrome is the fallback for console, network and JS reads only, and it is usually closed, so **ask
before reaching for it**.

## Orchestration

**Use nodeterm canvas orchestration, not the `Agent` tool** — invoke the `manage-nodeterm-canvas`
skill. Asher's standing preference for multi-agent work in this repo.

Two threads are genuinely independent and touch disjoint files, so they are worth running in
parallel worktrees as a bound group. Everything after them is sequential enough that splitting it
costs more in merge friction than it saves.

```
  ┌─ Thread A: gender ──────────────┐
  │  A1 → A2 → A3                   │  contracts, descriptors, WASM person components
  └─────────────────────────────────┘
                                       ── converge ──▶ B2 → B3 → C → B4 → B5
  ┌─ Thread B1: object store ───────┐
  │  AppHost container, chart, rclone│  AppHost.cs, Charts/, no app code
  └─────────────────────────────────┘
```

- **Thread A** touches `Shared.Contracts`, `Grpc/Features/People`, `Grpc/Data`, and
  `WASM/Features/People/Components/Individual`.
- **Thread B1** touches `AppHost.cs` and `Charts/impact-kids/templates/`.
- They overlap nowhere. Anything past the converge point shares `Person`, the photo endpoint or the
  WASM person components, so run it single-threaded.

Give each agent this doc plus the plan, and tell it which thread it owns. Have it report at slice
boundaries, not layer boundaries.

## Order of work

### Thread A — gender

Straight down the vertical slice; each is small.

- **A1** — `DbPerson.Gender`, `Person.Gender`, the two request objects, migration, and the
  `MudSelectCreateOrUpdate` in `PersonImpactDetails.razor`.
- **A2** — `_genderError` in `PersonImpactDetails.razor.cs`, ORed into `SendErrorsChanged`.
- **A3** — `GenderDescriptor`, `ElvantoPerson.Gender`, `ElvantoPersonFields.Gender`,
  `ElvantoService.GetPeople` and `CreatePerson`.

**`FieldNameParityTests` will do you a favour here.** It reflects over every `IFieldSyncDescriptor`
in the Grpc assembly and asserts each `FieldName` matches an EF property on `DbPerson`, because
`FieldChangeTrackingInterceptor` writes EF's property name into `FieldChangeLogs` and a mismatch
breaks the field's sync *silently*. So `FieldName` must be exactly `"Gender"`, `DbPerson.Gender`
must exist, and the descriptor must be constructible with no arguments. Run the test project after
A3; a red build here is the test working.

Ship the descriptor **`Bidirectional`**. The plan traces why that produces zero outbound rows at
seed time — do not "improve" it to `InboundOnly`, and do not add special casing for "take Elvanto
when local is null", because the reconciler already does exactly that.

**Verify A end to end in the app**, not just by building: sign in a child whose Elvanto gender is
blank, confirm the "Check Details" step goes red, set the gender, confirm it clears. Then run a sync
and read the `SyncPlannedChanges` rows to confirm gender appears as inbound/agreed and **not** as
outbound.

### Thread B1 — the object store

- SeaweedFS container in `AppHost.cs` for local dev, with a data volume and
  `ContainerLifetime.Persistent`. **Read
  [generated passwords and persistent volumes](../modules/infrastructure/generated-passwords.md)
  first** — a persistent data volume plus a regenerated credential is precisely the failure
  documented there, and it takes the whole stack down in a way that logs no cause.
- Hand-written templates under `Charts/impact-kids/templates/s3/`, in the idiom of the existing
  `sql` and `rabbitmq` ones. **Do not run `aspire publish`.** `k8s-artifacts/` is untracked, dead
  output; `Charts/impact-kids/` is the real chart.
- **No ingress, no YARP route.** Same rule as `PickupDisplayKeyEndpoints`' `internal/` group.
- The `rclone copy` CronJob and the Backblaze Secret.

### After the converge — and a standing instruction about the camera

**Do not halt on iOS camera behaviour, and do not ask for a device.** Asher will test the capture
view on a real phone once it is in production. There is no pre-build check, so do not build a
throwaway test page, do not block a slice on it, and do not raise it as a question.

What is required instead is that the capture view **treats camera failure as a normal state from its
first commit**, falling back to `<input type="file" accept="image/*" capture="user">` whenever
`getUserMedia` is absent, rejects, or opens a stream whose `videoWidth` never becomes non-zero
(give that one a timeout rather than an indefinite spinner). The plan's "build for it to fail"
section has the full list. Done that way, a standalone-mode surprise in production degrades the tool
instead of breaking it.

### After the converge

**B2** (serve endpoint) → **B3** (`PersonAvatar`) → **C** (upload, capture view, photos page,
`PhotoNeedsUpdate`) → **B4** (backfill worker) → **B5** (export).

C before B4 is deliberate: ~75–80% of children need a photo taken whatever the backfill achieves.

B3 lands as a legitimately unfinished slice — `PersonAvatar` with no photos to show renders exactly
as the current initial avatar. Say so when reporting it.

## Stop and ask

Per [AGENTS.md](../../AGENTS.md), only for what editing code cannot undo:

- **Backblaze credentials and the bucket.** Out-of-repo prerequisite. Ask Asher to create the bucket
  and supply keys; do not invent a bucket name and wire it in hopefully.
- **Enabling `Elvanto:AllowWrites`.** Not part of this work at all. If something seems to need it,
  stop — [the write gate](2026-08-elvanto-write-testing.md) governs it and it requires a written,
  human-approved verification of the exact plan about to execute.
- **A destructive migration** — dropping a column or table, narrowing a type, a backfill that
  rewrites existing rows. There are none in this plan; every column added is nullable and the photo
  table is new. If you find yourself needing one, that is a signal the design drifted, so say so
  rather than writing it.

## Migrations are yours to run

Worth spelling out because the restrictive reading of this costs a slice every time someone makes
it. **Additive migrations need no approval and no hand-off.** Write them, run them, carry on.

```bash
dotnet ef migrations add AddPersonGender --project GSBC.ImpactKids.Grpc
```

Name it for what it does, not a timestamp slug — the generated prefix already orders it.

Two things to know before you need them:

- **The undo is `./db-restore.sh`.** It drops the database, restores the newest prod dump in the
  repo root and re-applies migrations to head. That is why a bad additive migration on local dev is
  a minute of lost time rather than an incident.
- **`dotnet ef migrations remove` will probably fail, and that is not your bug.** It needs a live
  database connection, and `GsbcDbContextFactory` hardcodes port 60536 while a persistent container
  keeps whatever port it was first created with — so the two drift and you get
  `28P01: password authentication failed` or a refused connection. Delete the migration's two `.cs`
  files by hand and `git checkout -- GSBC.ImpactKids.Grpc/Data/Migrations/GsbcDbContextModelSnapshot.cs`
  instead.

Never suppress `PendingModelChangesWarning`. It is the only thing that tells you a model has
drifted from its migrations.

## Known loose ends

- `publish.sh` is deleted on this branch. `k8s-artifacts/` is untracked local output and can be
  removed from disk too; nothing references it once the script is gone.
- The root [AGENTS.md](../../AGENTS.md) says "this repo has no test projects". It is stale —
  `GSBC.ImpactKids.Grpc.Tests` exists with seven files covering the sync reconciler and descriptors.
  Fix that line while you are in there; it currently tells readers not to look for the safety net
  that caught the descriptor naming bug this plan depends on.
- When the work lands, fold both this doc and the plan per [docs/AGENTS.md](../AGENTS.md): durable
  facts into `docs/modules/people/` and `docs/modules/infrastructure/`, then move both to `archive/`
  with `folded_into:`. The Elvanto API measurements are already in
  [modules/elvanto/api-reference.md](../modules/elvanto/api-reference.md) and stay there.
