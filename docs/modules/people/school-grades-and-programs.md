---
title: School grades and who is in the program
kind: reference
status: current
module: people
verified: 2026-08-24
code:
  - GSBC.ImpactKids.Shared.Contracts/Entities/Features/People/SchoolGradeTiers.cs
  - GSBC.ImpactKids.WASM/Features/Attendance/Pages/Tool.razor.cs
  - GSBC.ImpactKids.WASM/Features/Attendance/Pages/SignIn.razor.cs
  - GSBC.ImpactKids.WASM/Features/People/Components/Individual/PersonDetails.razor.cs
---

# School grades and who is in the program

Which children the programme is for, and how the software decides. Read this before changing a grade
list, adding an age rule, or touching the order the attendance list puts people in.

**The grade on a child's record is often the thing that is wrong, not the child.** Every rule here
follows from that: age can override the recorded grade, an unknown age is never guessed upward, and
nothing in the sign-in path ever blocks.

## One list, in the contracts project

`SchoolGradeTiers` (`GSBC.ImpactKids.Shared.Contracts/Entities/Features/People/SchoolGradeTiers.cs`) is
the only definition of who is in the programme:

| Member | Contents | Meaning |
|---|---|---|
| `Program` | `Prep`, `1`–`6` | The programme, junior and primary together |
| `EarlyYears` | `Nursery/Pre-school`, `Kindergarten` | Below Prep — *not* the programme by label alone |
| `HighSchool` | `7`–`12` | Past the end of it |
| `EarlyYearsAndProgram` | the two above, spread | Every grade a memory verse can be logged against |
| `MinimumProgramAge` | `5` | The junior programme takes five year olds |

It lives in the contracts project because the same rule is read by the attendance tool, the memorisation
table and the sign-in warning. When two sides of a rule live in two files, they drift — the games reveal
proved that (see [../games/README.md](../games/README.md)), and a grade list is the same shape.

## Grades are matched by label, and the labels come from Elvanto

`DbSchoolGrade` has an `OrderNumber`, but every rule here matches on `Label` — the exact strings Elvanto
sends, including `Nursery/Pre-school` with its slash. Grades are not seeded locally; they arrive through
the Elvanto sync, so a renamed grade over there silently drops out of every list above. If a whole cohort
suddenly sorts to the bottom of the attendance list, check the labels first.

## The junior programme takes five year olds, whatever their grade says

`IsInProgram` (`SchoolGradeTiers.cs:90`) is true when the child's grade is in `Program`, **or** their
grade is in `EarlyYears` and they are at least `MinimumProgramAge`:

- The junior programme starts at Prep, which is age five — but a five year old is not always recorded as
  Prep. Some are still sitting in Kindergarten or Pre-school in Elvanto, and they belong in the room
  regardless.
- A **missing date of birth is not old enough.** An unknown age stays in the early-years tier rather
  than being guessed upward, because the cost of guessing wrong is a four year old in a room built for
  eight year olds.

Because age is per-person, the programme tier cannot be precomputed as a set of grade ids the way the
other tiers are — `ProgramGradesPersonFilter` asks the helper per person
(`Tool.razor.cs:148`), while the early-years and high-school filters still cache their id arrays.

## Where the tiers are used

### Attendance list ordering

`Tool.razor.cs:40` passes four tiered filters, in order: programme, early years, high school, no grade.
`ListComponent.TieredFiltering` (`ListComponent.razor.cs:151`) fills from the first tier, and only
reaches the next if it still has room under `Limit` — 15 on this page.

So the tiers are **search priority, not access control.** Everyone remains findable; the tiers decide who
a leader sees first when they type three letters of a name with a queue in front of them. Prep through
grade 6 sit in one tier together, which is what makes a Prep child as findable as a grade 6 one.

### Memory verses

`MemorisationEntriesTable.razor.cs:353` filters people to `EarlyYearsAndProgram` — a superset of the
programme, so early-years children can still be logged. There is no junior/primary split in the
memorisation table.

### Sign-in warning, and the age caption

- `OutOfProgramWarning` (`SchoolGradeTiers.cs:57`) returns a sentence, or null when the child fits. It
  names *which way* they fall out — too young, too old, or never recorded — because that is what decides
  what the desk does about it.
- `SignIn.razor:30` shows it as a warning alert above the stepper. **It never blocks.** The sign-in
  button stays live, and the copy says so: "Sign in anyway if that is right."
- `PersonDetails.razor:51` shows "Currently N years old" under the date of birth, tinted warning below
  `MinimumProgramAge`. It reads the date *in the form* rather than the saved record, so it tracks the
  picker as it changes — which is why `Person.CalculateAge` is static.

Verified against live data on 2026-08-24: a child recorded `Kindergarten` aged 5 is in the programme with
no warning; the same grade aged 4 warns; grade 7 warns.

## Changing the range

Edit `SchoolGradeTiers`, nothing else — every consumer reads from it. Two things to check when you do:

- A grade moving between `Program` and `EarlyYears` changes attendance ordering, not visibility. Nobody
  disappears.
- `MinimumProgramAge` is compared against `Person.GetAge()`, which is age *today*, not age at the
  service date. For a Sunday-night programme the difference never matters; if the app ever backfills
  historical attendance, it will.
