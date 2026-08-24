# Documentation

`docs/` carries the reasoning behind decisions that are not obvious from the code.
**[docs/README.md](docs/README.md) is the map** — read it before hunting through the tree.

Where things go, and where to read from:

- `docs/modules/<module>/` — how things work now. The **only** place to read from when answering "how
  does X work", and the place to update when a change alters documented behaviour. Always
  `status: current`.
- `docs/work/YYYY-MM-<slug>.md` — plans, handovers and design discussions. Write new ones here, never
  in `modules/`.
- `docs/open-questions/` — one unresolved question per file.
- `docs/archive/` — finished, rejected or superseded work. Read for *why*; never cite it as current
  behaviour, and never edit it.

Every doc has front-matter with `kind`, `status`, `code:` and `verified:`.

**Writing, moving or folding a doc — or changing code a doc describes — follow
[docs/AGENTS.md](docs/AGENTS.md).** It is the procedure: directory choice, front-matter schema, the
status lifecycle, and the rule that a behaviour change and its `modules/` doc update land in the same
commit.

Read before acting:

- [docs/modules/games/README.md](docs/modules/games/README.md) — before touching any scoring or display
  page. Why two display pages are anonymous and what may never be routed through the service they
  read, how the reveal keeps its step count in sync across the phone and the server, and why a race
  stores `Place` rather than inferring it from points.
- [docs/modules/auth/sign-in.md](docs/modules/auth/sign-in.md) — before changing anything in the YARP
  proxy or adding a route. **Two layers, not one:** a cookie in the browser and a bearer token the gRPC
  service validates. Also why being enabled is a database fact, and how the Development-only sign-in
  bypass is gated.
- [docs/modules/people/school-grades-and-programs.md](docs/modules/people/school-grades-and-programs.md)
  — before changing a grade list or an age rule. Age can override the grade on file, an unknown age is
  never guessed upward, and the attendance tiers are search priority rather than access control.

Per-area conventions, read before working in that project:

- [GSBC.ImpactKids.Shared.Contracts/AGENTS.md](GSBC.ImpactKids.Shared.Contracts/AGENTS.md) — contracts,
  base messages, `DateTime` at the boundary.
- [GSBC.ImpactKids.Grpc/AGENTS.md](GSBC.ImpactKids.Grpc/AGENTS.md) — service authorization, errors,
  EF traps, migrations.
- [GSBC.ImpactKids.WASM/AGENTS.md](GSBC.ImpactKids.WASM/AGENTS.md) — stores, MudBlazor, scoped CSS,
  the service worker.

# Local tooling

**Never `dotnet run`.** Run configurations only — `mcp__rider__execute_run_configuration`, per the
`run-and-inspect-app` skill. A CLI-launched app duplicates Rider's processes and fights over the ports
pinned in `launchSettings.json`, and it dies when the session that started it goes away, leaving a
half-running app nobody started deliberately. If the Rider MCP is unavailable, say so and ask.

This includes one-off side experiments: starting one project on a spare port to test a config gate is
still `dotnet run`. Use a run configuration, or ask for one.

Build through Rider rather than the CLI:

- build — `mcp__rider__build_solution`, which also works while the app is running
- per-file analysis — `mcp__rider__get_file_problems`, Rider's own inspections, so it catches more than
  the compiler

Rider is preferred because it returns just the problems instead of the ~75 pre-existing NU1902/MUD0002
warnings you then have to grep past.

`dotnet ef` is fine to run directly — there is no MCP equivalent. Migration *content* still follows the
rules in [GSBC.ImpactKids.Grpc/AGENTS.md](GSBC.ImpactKids.Grpc/AGENTS.md): additive is free, destructive
gets proposed first.

**Edit files with the Edit/Write tools, never with `sed` or a `python` heredoc.** A shell replace that
matches nothing fails silently and hides the diff — you get a green exit code and an unchanged file.

# Implementation Rule — Vertical Slices

Work in vertical slices, not in layers. A slice is one operation carried end-to-end: contract → DB
model → converter → service interface → service implementation → DI registration → frontend.

One operation per slice — "read multiple", "read one", "create", "update", "delete". Pick the order
that makes the next slice easiest to build and see working; reading before writing is usually that
order, but it is not a rule.

Do not batch a layer across operations (all contracts, then all services). Do not stop for approval at
layer boundaries.

## One slice, one reviewable change

Each slice is finished when it:

- builds — `mcp__rider__build_solution`
- registers what it added — `Program.cs` service mapping on the server, DI and client registration in
  the WASM app
- **has been seen working in the running app.** Use the `run-and-inspect-app` skill: start it through
  Rider, sign in with `/bff/dev-login`, drive the page, read the rows it actually wrote. This is a hard
  gate, not a nicety — it is where integration bugs surface, and this repo has no test projects, so it
  is the only gate there is.
- updates any `docs/modules/` doc whose documented behaviour it changed, per [docs/AGENTS.md](docs/AGENTS.md)

The only thing allowed through the gate unfinished is something a later slice in the same feature will
complete — a stubbed empty state, a disabled control, a placeholder count. Say so. Anything else that
does not work in the app is not a slice, it is work in progress.

If a slice's diff has grown past what one sitting can review, split it — usually backend first, then
frontend.

## Migrations and contracts

- **Migrations** — fine as long as they are non-destructive of existing data. Additive columns, new
  tables, widening types, new indexes: no approval needed, but the `dotnet ef` command is the user's to
  run. See [GSBC.ImpactKids.Grpc/AGENTS.md](GSBC.ImpactKids.Grpc/AGENTS.md).
- **Contracts** — change freely. They move often and every consumer is in this repo.

## When to stop and ask

Only for what cannot be undone by editing code:

- a migration that drops or rewrites existing data — dropping a column or table, narrowing a type, a
  destructive backfill. Propose it; do not run it.
- deleting or overwriting data outside a migration
- anything with an out-of-repo prerequisite — Auth0 configuration, Elvanto, a deploy-order dependency
- an ambiguity where two readings lead to materially different designs

Otherwise report at slice boundaries and keep going.

# Git

`master` is the trunk, and this is the one place the rules here are stricter than the habit in the
history.

Never:

- **commit on `master`** unless the user explicitly asks for it *and* you have told them plainly that
  the current branch is `master`. Both halves — the request and the acknowledgement — every time.
- **create branches.** The user makes them. When work needs one, say so and wait; do not branch and
  carry on.
- **push anything, anywhere.** No `git push`, no `--force`, no branch or tag pushes. Pushing is the
  user's call.

Reading is always fine: `git log`, `git diff`, `git merge-base`, `git status`.

On a branch the user has made — `feature/*` and the like — commit per slice without asking.
