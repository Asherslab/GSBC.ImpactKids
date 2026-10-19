---
title: Service schedules — templates, split groups, and changes on the night
kind: plan
status: in-progress
module: scheduling
opened: 2026-10-19
verified: 2026-10-19
code:
  - GSBC.ImpactKids.Shared.Contracts/Entities/Features/Scheduling/Schedules
  - GSBC.ImpactKids.Grpc/Features/Scheduling/Schedules
  - GSBC.ImpactKids.WASM/Features/Scheduling/Features/Schedules
---

# Service schedules — templates, split groups, and changes on the night

The running order of a service — "Start Night 18:30, Songs 10m, Lesson 15m …" — kept under the
service, built from templates, and changed live by leaders on phones during the night. It is the
ground the ProPresenter segment timers stand on ([2026-10-propresenter-agent.md](2026-10-propresenter-agent.md)):
those need to know what is on now and what is next, per group.

**Where it stands (2026-10-19).** Steps 1–4 below are built and were driven in the app: item templates,
schedule templates with groups and splits, a new service taking its type's default, applying a
template, clock times, +5 / −1 and the item dialog, with on-the-night changes landing as logged
adjustments (checked in the database). **Not yet seen working:**

- dragging, which the browser used for testing cannot do — it needs a phone and a desktop;
- typing a start time into the time field (`ScheduleTimeField`), for the same reason. The 18:30 in
  the local test data was set in the database.

Step 5, the per-group label feed and segment timing, has not started.

## What was asked for (2026-10-19)

- A schedule is a list of items under a **service**. Most services share one, so there are
  **schedule templates**.
- An item is a name and a time: either a **length** ("15 minutes") or a **clock time** it starts at
  ("Start Night — 18:30"). A clock-timed item still has a length.
- **Item templates** — "Song 1", "Song 2" — are placeholders that get a name filled in on the
  service ("Song 1 — Way Maker"), and later a song from a song list. Left unfilled, it reads "Song 1".
- Reordering is **drag and drop**, on phones, including during the night.
- On the night: add and take away time quickly (+5), add and remove items.
- Each item will record the **time it actually took**, for comparing against the plan over many
  weeks. That arrives with segment timing; the plan and the adjustments are kept apart now so the
  comparison is honest when it does.
- **Splits.** Groups sometimes run separately and simultaneously (Prep–3 doing songs and a lesson
  while 4–6 play games), and meet together for other parts. Any number of lanes, all under one
  service. Kids Life starts together with songs and then splits three ways.
- The label feed takes a **group** query parameter, so one ProPresenter text box can follow the
  younger groups and another the older.
- A template may be the default for **one** service type, and a new service gets its type's default.
  Any other template may be applied instead. Services are not synced with Elvanto.

## Decisions

| Question | Answer | Why |
|---|---|---|
| What does a clock time mean? | When the item **starts** | asked; "Start Night — 18:30" |
| Where do groups live? | Once per schedule; a lane covers one or more | asked (option A). Keeps `?group=` stable all night, through a two-way and then a three-way split |
| When does a split end? | When its longest lane finishes | asked |
| When does a change count as "on the night"? | Once the first item's time has passed | asked. Before that, a length change is just planning |
| Template applied to a service | **Copied**, not linked | editing a template must not rewrite past services, or the plan-vs-actual history lies |
| Templates and service schedules | One structure, `Schedule`, owned by either a service or nobody (a template) | one editor, one set of operations, and applying a template is a deep copy of the same shape |

## Shape

```
Schedule            a template (ServiceId null, has a Name, optionally a default ServiceTypeId)
│                   or a service's schedule (ServiceId set)
├── Group           "Prep–3", "4–6"          - ordered
└── Block           ordered
    └── Lane        ordered; one lane = together, two or more = a split
        │           a lane in a split lists the groups it covers
        └── Item    ordered: name, fill-in, planned minutes, starts-at, adjustment minutes
```

**A block with one lane is "together"**, whatever groups it lists: everybody is in it. A block with
two or more lanes is a split, and each lane names the groups it covers. Uniform on purpose — the feed
and the timeline walk one structure, not two.

### Time

- `PlannedMinutes` is what the schedule was set up with.
- `AdjustmentMinutes` is the sum of the changes made on the night; each change is also logged
  (`ScheduleItemAdjustment`: minutes, when, by whom).
- An item's length is the two added together.
- Actual start and end times arrive with segment timing.

One operation adds or takes away time, and the server decides which column it lands in: before
the night it changes the plan, during the night it is an adjustment. The phone does not get to choose,
so two phones cannot disagree about it.

The timeline — each item's clock start and end — is computed by one helper in Contracts, shared by
the phone, the server and (later) the agent:

- A clock-timed item starts at its time. If the items before it overrun that time, the clash is shown
  rather than anything being squeezed.
- Otherwise an item starts when the one before it in its lane ends.
- Every lane of a block starts when the previous block ends; the block ends when its longest lane does.
- Items before the first clock-timed item have no clock time, only their lengths.

### Editing by several phones at once

Every mutation locks the schedule's row (`SELECT … FOR UPDATE`) for its transaction, loads the
graph, changes it, renumbers positions and saves. Two leaders dragging at the same moment are applied
one after the other, never interleaved into a scrambled order. Each mutation ends with an event, so
every phone showing the schedule refreshes.

### What the phone may get wrong, and does not matter

A drag, a +5 and a group toggle are applied to the phone's own copy first (`ScheduleEdits`) and then
sent; the server's answer replaces the copy a moment later. Adding, removing and the dialogs wait for
the server instead. The phone guesses plan-versus-adjustment the same way the server decides it, so
the number does not jump when the answer lands.

### Drag and drop

SortableJS, vendored into `wwwroot/lib`. MudBlazor's drop container is built on HTML5 drag events,
which iPhone Safari does not deliver from a touch reliably, and this list is dragged on phones in the
middle of a programme. SortableJS has a touch path of its own and handles dragging between lists
(lane to lane, together to split).

## Order of work

1. Item templates — the flat library, admin page.
2. Schedule templates — the structure, its editor, groups, blocks, lanes, items, dragging.
3. A service's schedule — applied from a template, the service type default, the service page.
4. The night — the timeline with clock times, +5 / −1, adjustments logged.
5. Then segment timing and the per-group label feed, in the ProPresenter plan.
