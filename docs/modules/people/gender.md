---
title: Gender
kind: reference
status: current
module: people
verified: 2026-08-29
code:
  - GSBC.ImpactKids.Grpc/Features/People/Sync/Descriptors/GenderDescriptor.cs
  - GSBC.ImpactKids.Grpc/Data/Models/People/DbPerson.cs
  - GSBC.ImpactKids.Shared.Contracts/Entities/Features/People/Person.cs
  - GSBC.ImpactKids.WASM/Features/People/Components/Individual/PersonImpactDetails.razor
---

# Gender

A synced field like any other, with one thing worth knowing before changing it: **null is a real
answer, and about a third of children have it.**

## The three states

`Person.Gender` is a nullable `Gender` enum with exactly two members, `Male` and `Female` — the only
values Elvanto returns. "Not told" is the *absence* of the enum rather than a member of it.

That is deliberate. An `Unknown` member reads as a value, and a value is something code eventually
pushes to Elvanto as though a human had chosen it. Stored on `DbPerson` as a nullable string, the
same way `MediaConsent` is, because the sync descriptor reads and writes it through
`IFieldSyncDescriptor`'s string interface.

**The EF property must stay named `Gender`.** `FieldChangeTrackingInterceptor` writes EF's property
name into `FieldChangeLogs`, so a rename breaks the field's sync silently — no error anywhere.
`FieldNameParityTests` turns that into a red build.

## At sign in

A missing gender reddens the "Check Details" step and shows the profile-errors alert, alongside the
existing media-consent and first-time checks. **Expect it on roughly a third of children**: 35% of
the children with a school grade have no gender in Elvanto. That is the point of the field, but it is
worth telling leaders before it lands rather than after.

Guardians see no gender field at all, because `PersonImpactDetails` only renders for non-guardians.
The sync still stores gender for them; it is simply not shown or enforced. The error exists to stop a
child being signed in against an incomplete profile.

## Syncing

`GenderDescriptor` is `Bidirectional`, and the reason is worth keeping because it looks wrong at
first glance.

At seed time every app-side gender is null, so `FieldReconciler.Decide` finds no base row and falls
to `DecideFirstSync`, where `appHasSomethingToSay` is false for every person — the two branches that
produce outbound rows are unreachable. Elvanto holding `"Male"` lands inbound; Elvanto holding `""`
settles as agreed, with no row and no noise. **A `Bidirectional` gender descriptor therefore plans
zero outbound writes at seed time.** Measured on a real plan: 1227 inbound, 1 outbound — and the one
was a child whose gender a leader had filled in against an Elvanto blank.

So "take Elvanto's value when the app holds null" needs no special casing: it is already what the
reconciler does. Do not "improve" this to `InboundOnly`.

Outbound rows appear later and from one source only — a leader filling in a gender Elvanto has
blank — which is the end state the field exists for. The obligation that creates is a reporting one:
whoever next runs the Elvanto write gate will see gender rows in the plan and must approve them
knowingly.

`""` from Elvanto is refused by the base `IsValidInboundValue`, so Elvanto holding nothing can never
clear a gender a leader typed here.

## Reading it from Elvanto

**`gender` must be named in the `fields` array on `people/getAll`.** It is not returned by default,
and leaving it out is silent: the call succeeds, the field binds null for every person, and the sync
does nothing while looking healthy. This is the exact inverse of `picture`. See
[the Elvanto API reference](../elvanto/api-reference.md).

On a write it travels nested under `fields`, never at the top level.
