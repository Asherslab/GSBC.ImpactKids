---
title: Documentation map
kind: reference
status: current
verified: 2026-08-24
---

# Documentation

Start here. Every doc carries front-matter saying what it is (`kind`) and whether it is still true
(`status`), so you can tell reference from a plan without reading it.

Writing or moving a doc rather than reading one? [AGENTS.md](AGENTS.md) is the procedure — which
directory, what front-matter, and how a doc leaves `work/`.

Operating the repo — running the app, driving the browser, reading the database — is not here. That is
`.claude/skills/run-and-inspect-app`, and the repo-wide rules are in [../AGENTS.md](../AGENTS.md).

## For how something works, read `modules/` only

Present tense, kept current, no dates.

| Module | Docs |
|---|---|
| **games** | [team points, wall displays and the reveal](modules/games/README.md) |
| **auth** | [the cookie, the bearer token, and the local bypass](modules/auth/sign-in.md) |
| **people** | [school grades and who is in the program](modules/people/school-grades-and-programs.md) |
| **infrastructure** | [generated passwords and persistent volumes](modules/infrastructure/generated-passwords.md) |

## The other three directories

| Directory | What is in it | Trust it for |
|---|---|---|
| `work/` | plans and discussions for changes not yet finished | what someone intends to do |
| `open-questions/` | one unresolved question per file | knowing something is *not* settled |
| `archive/` | finished, rejected or superseded work | history and reasoning only — **never** current behaviour |

### In flight now

- [Elvanto sync — base values, plan-then-execute, and splitting the orchestrator](work/2026-08-elvanto-sync-refactor.md)
  — the plan. Read [the findings register](work/2026-08-elvanto-sync-review.md) first if you want
  the evidence rather than the response.
- [Elvanto sync — a persisted local-family ⟷ Elvanto-family mapping](work/2026-08-elvanto-family-mapping.md)
  — handover for the family-id work: why 494 people diverge on `FamilyId` every run, and the table that
  ends it. **Built and verified**; also covers "no family" becoming a real state.
- [Elvanto sync — full-feature test, ending in real writes](work/2026-08-elvanto-write-testing.md)
  — the test plan, and **the halt-and-verify gate that must be satisfied before any write reaches
  Elvanto**. Read the gate before touching `Elvanto:AllowWrites`.
- [Elvanto sync — the plan table shows raw ids for translated fields](work/2026-08-sync-plan-table-identifiers.md)
  — `proposed`, not started. Why a school grade row shows two different GUIDs for the same grade, and
  the three ways to fix the reading problem without changing what gets sent.

### Open

Nothing. Add an entry when you open an `open-questions/` doc.

## Lifecycle

A doc has one `status`, and one way out of it.

```
proposed ──accepted──> in-progress ──landed──> folded ──> archive/
    │                       │
    └──rejected─────────────┴──superseded────> archive/
```

- `modules/` docs are always `status: current`. They are rewritten in place, never dated, never
  superseded.
- `work/` docs are `proposed`, `accepted`, `in-progress` or `landed`. When the change lands, the durable
  facts move into `modules/` and the file moves to `archive/` with `folded_into:`.
- `archive/` docs are `folded`, `rejected` or `superseded`. Never edited — supersede instead.
- `open-questions/` docs are `open`. Closing one means deleting it and updating the module doc.

`verified:` is the last date someone checked a doc against the code. If the code in `code:` has changed
since, treat the doc as suspect and fix it while you are there.

## Naming

- `modules/<module>/<topic>.md` — no dates, no version words.
- `work/YYYY-MM-<slug>.md` and `archive/YYYY-MM-<slug>.md` — dated by when the work opened.
- `open-questions/<slug>.md` — no dates.
