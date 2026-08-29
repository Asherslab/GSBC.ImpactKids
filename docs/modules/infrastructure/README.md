---
title: Infrastructure
kind: reference
status: current
module: infrastructure
verified: 2026-08-29
code:
  - GSBC.ImpactKids.AppHost/AppHost.cs
  - Charts/impact-kids
---

# Infrastructure

The containers the app runs alongside — locally through `AppHost.cs`, in the cluster through
`Charts/impact-kids/`.

| Doc | Read it before |
|---|---|
| [Generated passwords and persistent volumes](generated-passwords.md) | deleting a data volume, or changing a password parameter. A container that seeds its password only on an empty data directory will not follow the AppHost, and the symptom is the whole stack failing to start with nothing logging a cause. |
| [The photo object store](object-store.md) | touching `Charts/impact-kids/templates/s3/`, the SeaweedFS container, or the Backblaze backup. Why it has no ingress, why the backup job's credential cannot write, why the SeaweedFS volume flags are not tuning, and the one word in the backup command that must never become `sync`. |

Two things that are true of both, and are the usual way to lose an afternoon here:

- **`Charts/impact-kids/` is hand-written.** It is not generated. `k8s-artifacts/` is the output of
  `aspire publish`, is untracked, is deployed by nothing, and has already diverged in shape. Do not
  run the publisher and do not believe its output.
- **Never `dotnet run`.** Run configurations only, per [../../../AGENTS.md](../../../AGENTS.md) and
  the `run-and-inspect-app` skill.
