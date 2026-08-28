---
title: Elvanto sync — the plan table shows raw ids for translated fields
kind: plan
status: proposed
module: sync
opened: 2026-08-27
verified: 2026-08-27
code:
  - GSBC.ImpactKids.WASM/Features/Sync/Components/PlanTable.razor
  - GSBC.ImpactKids.Grpc/Features/People/Sync/Services/ElvantoPersonSyncService.Fields.cs
  - GSBC.ImpactKids.Grpc/Data/Models/Sync/DbSyncPlannedChange.cs
---

# The plan table shows raw ids for translated fields

Two fields are compared in one vocabulary and pushed in another. The plan table shows both columns
raw, so a reviewer reads two unrelated-looking GUIDs and has to already know the translation to tell
whether the row is correct.

## The evidence

A real row from a Decide run on 2026-08-27:

| Person | What | Field | App | Elvanto | Proposed | Reason |
|---|---|---|---|---|---|---|
| Asher George | Field to Elvanto | SchoolGradeId | `6f415858-1a03-41e0-898e-408ac6996362` | `-` | `561549f2-7892-11e2-aee1-65d3f34685c6` | `FirstSync:ElvantoHasNothing` |

Both GUIDs are **the same grade**, "6":

```
SchoolGrades.Id 6f415858-1a03-41e0-898e-408ac6996362 | Label 6 | ElvantoId 561549f2-7892-11e2-aee1-65d3f34685c6
```

The row is correct. It is just unreadable without the lookup, and the first question a reader asks is
whether the app is about to push the wrong value.

## Why the columns disagree

`BuildComparison` puts both sides of a field into **one comparison space**, and for `FamilyId` and
`SchoolGradeId` that space is the app's — see the comment on `comparedApp`, and the incident it
records. The observed columns are therefore local Guids.

The wire is not in that space. `OutboundValue` translates back to what Elvanto will actually be sent:
an Elvanto household id, or a `DbSchoolGrade.ElvantoId`. `ProposedValue` is that translated value,
because it has to be — it is what the payload will carry.

So the mismatch is not a bug to remove. **The Proposed column must keep showing the value that will
be sent**; a plan that displays something other than what it will do is worse than an unreadable one,
and debugging a rejected push means seeing the exact string. The problem is only that nothing on the
row says the two columns speak different languages.

Affected fields, and only these two:

- `FamilyId` — local family Guid observed, Elvanto household id (or `new`) proposed.
- `SchoolGradeId` — local grade Guid observed, `DbSchoolGrade.ElvantoId` proposed.

Every other field is the same string in both spaces, which is why this has gone unnoticed.

## Options

1. **Render a label beside the id, in the UI only.** `PlanTable` resolves a `SchoolGradeId` to its
   `Label` ("6") and a `FamilyId` to the family's name, showing the id as secondary text or a
   tooltip. Nothing stored changes; the audit rows and `DbSyncPlannedChange` keep raw values.
   Cheapest, and it fixes the reading problem where the reading happens. Needs the grade table and
   family names available to the page, which `Individual.razor.cs` does not currently load.
2. **Carry display text on the contract.** Add nullable `AppDisplay` / `ProposedDisplay` to
   `SyncPlannedChange`, filled by the orchestrator, which already holds both lookups at the moment it
   builds the row. Costs a contract field and a migration if persisted; benefits the audit trail and
   the "would push" log lines too, which have the same wart.
3. **Do nothing, document it.** This file.

Option 1 is the recommendation if this stays a UI complaint. Option 2 is right if the same confusion
turns up when reading `SyncAuditLogs` directly, which is where a failed run is actually diagnosed.

## What must not change

- `ProposedValue` keeps holding the exact string sent to Elvanto.
- The comparison stays in the app's terms for both fields. Comparing family in Elvanto's terms is the
  thing that made a household split across two Elvanto families disagree with itself forever.
