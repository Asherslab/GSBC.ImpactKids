---
title: Sign-out night — what the pickup plan misses
kind: discussion
status: proposed
module: attendance
opened: 2026-08-27
verified: 2026-08-27
code:
  - GSBC.ImpactKids.WASM/Features/Attendance
  - GSBC.ImpactKids.Grpc/Features/Attendance
  - GSBC.ImpactKids.WASM/Components/Multiple/ListComponent.razor.cs
  - GSBC.ImpactKids.WASM/Features/Games/Services/GamePointsService.cs
  - GSBC.ImpactKids.YARP/appsettings.json
---

# Sign-out night — what the pickup plan misses

A response to [the pickup plan](2026-08-pickup-requests-and-activity-log.md). The three pieces it names —
activity log, `Requested` state, pickup wall — are settled and are not re-argued here. This doc is the
rest of the night: the two things in the plan that will not survive contact with a real 7:40pm, the
things already in the attendance pages that make the night harder, and what I would build after that.

Read the ranking as a ranking. If there is one sitting for this, it is items 1–3.

---

## 1. Nobody owns the YARP route, and without it the wall renders "Connecting…" forever

**Blocker. Small. Do this first.**

The plan's station table gives the backend station `Grpc/Program.cs` and the wall station
`WASM/Program.cs`. Neither owns `GSBC.ImpactKids.YARP/appsettings.json`, and the `public` route there is
pinned to one service by name, not by prefix:

```json
"public": {
  "_comment": "Anonymous on purpose - ... Only the read-only scoreboard service is routed here.",
  "Match": { "Path": "/public/GSBC.ImpactKids.Games.Display/{**catch-all}" }
}
```

`GSBC.ImpactKids.YARP/appsettings.json:30-36`. A call to `public/GSBC.ImpactKids.Attendance.Display`
does not match it. It falls through to the `wasm` catch-all at line 37-42 and gets `index.html` with a
200 — which is exactly the failure
[docs/modules/auth/sign-in.md](../modules/auth/sign-in.md) already documents as
`Bad gRPC response. Invalid content-type value: text/html`, and which reads as a serialisation bug rather
than a missing route. On a wall with nobody standing at it, it reads as "Connecting…" and nothing else.

**Cost:** one route entry in `GSBC.ImpactKids.YARP/appsettings.json`, plus a row in the proxy-route table
at `docs/modules/auth/sign-in.md:36` — that doc's table is normative and a new anonymous route must appear
in it in the same commit, per [docs/AGENTS.md](../AGENTS.md).

**What could go wrong:** the tempting fix is to widen the match to `/public/{**catch-all}`, which turns
"one named anonymous service" into "anything anyone ever routes under `public/`". That is the opposite of
the plan's own privacy contract. Add a second named route; leave the games one alone.

**Ownership:** give it to the backend station explicitly. It is currently in nobody's file set, which is
how it gets found on the night.

---

## 2. The toggle's "clear" is the same tap target as the request — and it will clear a real request

**Small. This is a correctness problem in the planned design, not an addition to it.**

The plan says: *"Requesting is a toggle — pressing it again clears the request."* Two leaders on two
phones is the case the whole feature exists for, so run it:

- Leader A taps Jonah. The row on A's phone becomes "requested".
- The event reaches B's phone (`eventService.SendUpdatedEvent` → SSE → `RefreshEvent`). B's row now also
  reads "requested" — and B's button is now a **clear** button.
- B, who is looking at the queue rather than the screen, taps Jonah because Jonah's parent is standing in
  front of *them*. The request is cleared. Jonah's name leaves the wall. Nobody at the desk knows.

The same shape happens on one phone: a leader who taps, gets distracted, comes back and taps again to
"make sure" un-does it. A control designed to be *"tapped without thinking"* must not have a destructive
second meaning on the same pixel.

**What I would do instead:** keep the toggle in the contract — `RequestPickupAttendanceRecordRequest` with
its `Requested` bool is right, and the server staying idempotent is right. Change the *affordance*:

- Requesting is the big tap target on the row.
- Once requested, that target becomes inert-and-informative ("Requested 7:41 · Sam") and clearing moves to
  a small, separate ✕ on the chip.
- A second tap on the big target is a no-op that says "already requested at 7:41 by Sam" rather than
  undoing it.

**Cost:** small, and entirely inside the attendance-page station's new component under
`WASM/Features/Attendance/Components`. No contract change.

**What could go wrong:** a small ✕ is a small target on a phone held one-handed at a desk, so a genuine
mis-request now takes two attempts to undo. That is the correct trade — the cost of a failed clear is
three seconds, the cost of an accidental clear is a child nobody is fetching.

---

## 3. The night is family-shaped; every screen in the plan is child-shaped

**Medium. The biggest usability win available.**

Parents do not arrive per child. A parent with three children in the room means, under the plan as
written: three request taps, three names on the wall, and then three separate passes through the sign-out
stepper. Count the taps for the third child today (`SignOut.razor`, `Family.razor`):

1. Type in the search box (`Tool.razor:72-83`) — fuzzy, threshold 80.
2. Tap the person card → `/Attendance/Family/{id}` page load.
3. Tap **Sign Out** → `/Attendance/SignOut/{id}` page load, landing on step 0, "Check Details"
   (`SignOut.razor:35-47`).
4. Tap **Next** into "Sign Out" — *unless* `PersonDetailsErrorsChanged` happened to fire clean first, in
   which case `_index` was set to 1 for you (`SignOut.razor.cs:186-196`). It fires once, guarded by
   `_firstError`; a child whose profile has a flagged field never gets the skip, so the child most likely
   to slow you down costs the extra tap.
5. Tap **Sign Out**.
6. Tap **Next** through "Check Items" to leave.

Five taps and three page loads per child, times three children, times forty families in the last ten
minutes. The stepper is right for a first-time sign-*in*. It is the wrong instrument for the fortieth
sign-out of the night.

**What I would build:**

- **On `/Attendance/Family/{id}`: one "Request all" and one "Sign out all" for everyone in the household
  currently signed in.** The page already has exactly the data — `_peopleAttendance` is a
  `Dictionary<personId, attendanceRecordId>` of the signed-in members (`Family.razor.cs:137-141`) — so
  both are a loop over `AttendanceRecordsService.Update(new SignOutAttendanceRecordRequest { … })`. The
  button labels the count: "Sign out all 3".
- **The wall groups a family's names onto one line.** `PickupDisplayEntry` carries no id by design, so the
  grouping must happen server-side in the display service, which has `DbPerson.FamilyId` in hand:
  `"Jonah, Ella & Mia P."`. Fewer lines, larger type, and the room fetches a household in one trip.
  Note the trap in `Person.HasFamily` (`Person.cs:28-40`): `FamilyId == Guid.Empty` is "no household" and
  several hundred people share it — grouping on it unfiltered produces one enormous fake family. Group
  only where `FamilyId != Guid.Empty`.

**What could go wrong:**

- **"Sign out all" skips the item check.** The stepper's third step exists to hand back bags —
  `AttendanceItemRecordDisplay ShowReturnError` (`SignOut.razor:87`). Gate the batch button: offer it only
  when no member of the household has an outstanding item record, and fall back to the per-child stepper
  when they do. Say which child is holding it up.
- **Two children of one household are collected by two different adults** (separated parents, an older
  sibling taken by a grandparent). "Sign out all" then records a sign-out that did not happen. The batch
  must be a *default*, never the only path — the per-child buttons stay exactly where they are.
- **Grouped names on the wall are longer**, and the `--rows`/`--scale` idiom the plan borrows from
  `ScoreDisplay.razor:49` scales by row count, not by line length. A five-child household on one line will
  overflow before a ten-row board does.

---

## 4. Make the filter chips count, and make an empty list distinguishable from a broken one

**Small. Nearly free, and it absorbs most of "what does the app say at the end of the night".**

The plan's piece C is a `Requested` filter. Adding it to the existing `_filters` dictionary inherits two
problems that are already live:

**a. A null store shows everyone, and calls them signed in.** `Tool.razor.cs:50-55`:

```csharp
_filters["Signed In"] = x => _attendanceRecords.Data?
    .Any(y => y.PersonId == x.Id && y.LocalSignedOut == null) != false;
```

`Data` is null while loading and stays null when the store is in its failure state
(`RefreshableStore.cs:38-45`). `null != false` is **true**, so every filter degrades to "show the entire
roster". At 8:15pm, "Signed In" showing 900 names and "Signed In" showing 3 names look like the same
control doing its job. A `Requested` filter written the same way means an empty wall and a broken phone
are indistinguishable — which is the one distinction the end-of-night sweep exists to make.

**b. The sweep silently truncates at 15.** `Tool.razor:93` sets `Limit="15"`, and with `TieredFilters`
present `ListComponent.TieredFiltering` (`ListComponent.razor.cs:150-181`) fills tier by tier and stops.
Twenty children outstanding shows fifteen, with no count and no "showing 15 of 20". A sweep that quietly
hides five is worse than no sweep.

**What I would build:**

- Filter chips carry counts: `All · Signed In 38 · Requested 4 · Signed Out 12`. The desk then sees where
  the work is without changing screens, and at 8:20pm "Signed In 3" *is* the end-of-night answer.
- The counts come from `_attendanceRecords` directly, so `Data == null` renders a dash, not a number.
- Fix the filters to `== true`, and lift `Limit` when a state filter is active — the signed-in set is
  forty people, not nine hundred.

**Also, while in there: `AttendanceOverview` miscounts a re-signed-in child.**
`AttendanceOverview.razor.cs:69-71` is `DistinctBy(x => x.PersonId).Count(x => x.LocalSignedOut == null)`.
`DistinctBy` keeps the **first** record per person in enumeration order, and the server returns records
ordered by `SignedIn` ascending (`AttendanceRecordServices/ReadMultiple.cs:31-32`). A child signed in,
signed out by mistake, and signed back in is counted by their *first* record — signed out — so the number
the leader reads to decide the building is empty says the child has gone while they are still in the room.
Count over the latest record per person, the way `Family.razor.cs:141` already does with
`MaxBy(x => x.SignedIn)`.

**What could go wrong:** counts recompute on every store push, and the store pushes on every sign-out
across every phone. Forty recomputes over a nine-hundred-person roster inside a Blazor WASM render loop is
not free — compute the four counts once per store change, not once per chip per render.

---

## 5. The anonymous surface is wider than the plan's privacy section thinks

**Small. And I do think the plan is right about names on the wall — the argument is elsewhere.**

I would not overturn the decision to put display names on the screen. The plan's reasoning is sound: the
screen is in a room full of the parents of those children, the service is separate from
`IGameDisplayService`, and the payload carries no id. Keep all of it. Two things it does not address,
though, are the parts that actually carry risk.

**a. The URL is the whole control.** `/Display/Pickup` is `[AllowAnonymous]` on a WASM app served by the
YARP `wasm` catch-all with no policy (`GSBC.ImpactKids.YARP/appsettings.json:37-42`). Once item 1 adds the
`public/` route, a live list of which children are in a known building at a known hour is served to anyone
who types the URL. For the games board that leak is a score; here it is a roster. The plan's rules govern
*what a name looks like* and say nothing about *who can ask*.

What I would do, in order of preference:

1. **Serve names only inside a window around the service** — `DbService` has the date; return
   `Waiting = []` outside, say, the two hours around it. A guessed URL on a Tuesday returns "Nobody
   waiting", which is both true and useless to a stranger. Cost: a few lines in the display service.
2. Drop the bare `/Display/Pickup` route and keep only `/Display/Pickup/{ServiceId:guid}`, so the URL is
   not guessable. Cost: also small — but the TV's bookmark then changes every week, which is a real
   Sunday-night cost and the reason `/Display/Scores` does not do this. I would take (1) alone.

**b. "First name plus last initial" fails exactly when it matters.** Two children called Jonah whose
surnames both start with P — not rare in a church with large families — appear on the wall as two
identical lines, and the room sends the wrong child to the door. The anonymisation *causes* the
safeguarding failure it was meant to prevent. Fix it server-side, where `Waiting` is built: when a display
name collides with another entry in the current waiting list, expand both to the full surname. Nothing
else changes; ambiguity is resolved only when it exists.

**What could go wrong:** the time window is another thing that can be wrong on a night that starts late or
runs long — make it generous, and make an out-of-window response render the deliberate "Nobody waiting"
screen rather than an error, per the plan's own empty-state rule. The collision rule leaks a full surname
in precisely the case where both families are in the room, which is the acceptable direction of the trade.

---

## 6. The wall needs to say something about itself that a list of names cannot

**Small to medium. All of it inside `PickupDisplay.razor` and the display service.**

Three things the room leader needs that the plan's ordered list does not give them:

- **Which name is new.** The plan orders `Waiting` by `RequestedAt` ascending, longest wait first — right
  for chasing, wrong for noticing, because a new name appears at the *bottom* of the board, the least
  watched part of a wall. A brief highlight on anything requested in the last ~20 seconds costs a CSS
  animation and a client clock; `RequestedAt` is already on `PickupDisplayEntry`.
- **How long a name has been up.** Position gives order, not age. "4 min" beside a name is the difference
  between a queue and a problem.
- **Whether the board is still live.** `WatchPickups` copies `GameDisplayService`'s 15s tick and 30s
  keepalive. If the stream dies, the score board goes stale harmlessly; **the pickup board goes stale
  dangerously** — the room keeps sending children to the door for names that were signed out ten minutes
  ago, and never sees the new ones. The wall should mark itself: if no push has arrived in ~90s, dim the
  list and say so. The room reading "not updating" is recoverable. The room trusting a frozen list is not.

**What could go wrong:** the games doc's animation rule applies directly here —
[docs/modules/games/README.md](../modules/games/README.md) records that the reveal's ticker had to be
guarded on the step actually changing (`_countedStep`) because the stream re-sends the same board every
30s and restarting on a keepalive springs everything back to the start. A "new name" highlight keyed on
the *response* rather than on the entry will re-flash every name every keepalive, which is a wall that
strobes. Key on the entry.

---

## 7. A tap on a stale row reports success and means nothing

**Small.**

The plan makes requesting an already-signed-out record a **no-op success** — correct, an error at the desk
mid-pickup helps nobody. But the desk then gets a green result for an action that had no effect, and the
child never appears on the wall, so the leader waits for someone who left ten minutes ago.

The same hole already exists in sign-out: `SignOut.razor.cs:147` guards the call with
`_attendanceRecord.Data is { LocalSignedOut: null }`, so a second leader signing out the same child taps
"Sign Out", sends nothing, and is advanced to the next step with no message at all.

**What I would build:** in both cases, when the client's action was absorbed, say what the record actually
holds — "Ella M. was signed out at 7:42 by Sam". The data is already in the store; the record carries
`SignedOutUserId`, and the activity log (piece A) is building the same name lookup.

**What could go wrong:** a snackbar per absorbed tap on a busy desk becomes noise the leader learns to
dismiss without reading. Make it in-row and persistent rather than transient.

---

## 8. Sign-out still writes every column, which the new columns make unsafe

**Small. Backend station, in a file it already owns.**

[GSBC.ImpactKids.Grpc/AGENTS.md](../../GSBC.ImpactKids.Grpc/AGENTS.md) is unambiguous: *"Two phones on the
same night is exactly that situation. Write only the columns the code owns."* The plan applies that rule
to the new `RequestPickup` operation and leaves the existing writers as they are:

- `AttendanceRecordServices/Update.cs:31` — `db.AttendanceRecords.Update(attendanceRecord)` on sign-out.
- `AttendanceRecordServices/Delete.cs:22` — same, on the soft delete.

Both now emit an `UPDATE` listing `PickupRequested` and `PickupRequestedUserId` with the values read
milliseconds earlier. The window is small, but this feature is the thing that makes the row genuinely
multi-writer — the desk and the door, on two phones — and it is two lines per file:

```csharp
db.Entry(attendanceRecord).Property(x => x.SignedOut).IsModified       = true;
db.Entry(attendanceRecord).Property(x => x.SignedOutUserId).IsModified = true;
```

**What could go wrong:** the AGENTS rule's own second clause — mark a property only when it actually
changed, or you write the stale read back in a smaller window. Both properties here are assigned
unconditionally on the line above, so both are safe to mark.

---

## 9. The desk's writes are online-only; the games half of this app already solved that

**Medium. I would build the indicator now and the outbox later.**

`RefreshableStore.RefreshAll` caches for 30 minutes (`RefreshableStore.cs:44`), so a phone that loses
reception mid-night keeps *reading* fine — the roster and tonight's records are still there, the page looks
healthy. Every *write* fails to a snackbar (`SignOut.razor.cs:160-164`) and is lost. Meanwhile
`GamePointsService` has a localStorage-backed outbox (`GamePointsService.cs:41-44, 877-914`) and
`wwwroot/js/connectivity.js` reports online/offline transitions, both built for exactly this hall.

The pickup request is the easiest thing in the app to queue: it is an idempotent set-a-flag toggle with no
create/delete pairing, so it needs none of the three tombstone rules the games doc had to arrive at. Sign-out
is genuinely harder — it stamps a timestamp, and a safeguarding record written from an unsynchronised phone
clock is a different kind of artefact.

**What I would build first, and it is cheap:** a visible connection state on `/Attendance/Tool` itself. The
only current indicator is a 24px wifi icon in the app bar (`MainLayout.razor:16`), which nobody at a desk
looks at. A leader must know that the request they just tapped did not reach the wall, because the
consequence is a room that never hears the name.

**What could go wrong with the full outbox:** a request queued offline is invisible to the room, so the
desk believes a child has been called who has not been. A queue is only safe if the UI distinguishes *sent*
from *queued* at the row, not in a global badge. That is most of the work, and it is why this is ranked
last rather than skipped.

---

## Already making the night harder — the small ones, with citations

| Where | What | Fix |
|---|---|---|
| `Family.razor.cs:62-71` | With no `ServiceId` in the query string this matches `LocalDate.Date == DateTime.Today` and **does not** fall back to the most recent service, unlike `Tool.razor.cs:99-101`. Past midnight, or on a service dated wrong, `_peopleAttendance` copies the failure (`:130-135`), the `else if` at `Family.razor:77` is false, and the page renders the family with **no sign-in and no sign-out buttons at all** — and no alert for it, since only `_service` and `_person` errors render. Only bites on a bookmark or a back-button, since every link into the page carries `ServiceId`. | Mirror Tool's fallback; render the record-level error. |
| `Tool.razor.cs:106-108` | The two error strings are swapped: a null `ServiceId` reports "Failed to find Service for Id" and a supplied one reports "…for Today". Same inversion at `Family.razor.cs:74-77` and `SignIn.razor.cs:96-98`. It is the message a leader reads when the night will not load, and it points at the wrong problem. | Swap them. |
| `Tool.razor:91` | The default list is ordered by **first** name across the whole roster under `Limit="15"`, so before anyone types, the desk sees fifteen alphabetically-early children and nothing useful. Search is doing all the work. | Covered by item 4 — counted chips make "Signed In" a one-tap default worth landing on. |
| `SignOut.razor:12` | `<PageTitle>Sign In</PageTitle>` on the sign-out page. | One word. |

---

## What I would not build

- **A "collected" acknowledgement from the room.** [docs/modules/games/README.md](../modules/games/README.md)
  states the rule plainly — *"The displays take no input"* — and it is right for signage nobody stands at.
  Worse, a room-side ack creates a fourth state that the desk did not write, which is precisely the
  conflation of "called for" and "gone" that this feature exists to end. The acknowledgement is the child
  arriving at the desk.
- **An audible cue on a new name.** A TV in a room of forty children, where ProPresenter owns the audio.
  The visual emphasis in item 6 is the whole of the answer.
- **Grouping the wall by room or grade.** There is no room concept anywhere in the contracts — I looked;
  `Entities` has no such type — so this would mean inventing one. The only grouping available is
  `SchoolGrade` / `SchoolGradeTiers`, and putting a grade on the wall is more personal data than the plan's
  contract permits and re-opens a decision that has been made well. Group by family (item 3) instead: it is
  the grouping the night actually has.
- **A "left without sign-out" state for children still signed in at the end.** Tempting, and unnecessary:
  the leader signs them out late, and the activity log plus the timestamp already makes a 21:14 sign-out
  legible as what it was. A new state costs a migration, a filter, a wall rule and a conversation about
  what it means for the record, to express something two existing columns already say.
- **Notifying parents.** Out of repo, out of scope, and a phone number in `Person` is not consent to text it.

---

## Where these contradict an existing rule

- **Item 1** widens the anonymous proxy surface. `docs/modules/auth/sign-in.md:36` documents the `public/`
  route as *"anonymous on purpose"* for the wall display specifically, and the JSON comment at
  `GSBC.ImpactKids.YARP/appsettings.json:31` says *"Only the read-only scoreboard service is routed here."*
  Both are now false. They must be edited, in the same commit, to say that two named services are routed
  there and why — not left to be read as still true.
- **Item 3's family grouping on the wall** puts a shared surname on an anonymous screen. That is inside the
  plan's stated contract ("first name plus last initial") only if you read `"Jonah, Ella & Mia P."` as three
  display names sharing one initial. I think that reading is honest, but it is a widening of the wall's
  payload and should be agreed rather than assumed — the plan says so itself: *"Widening it is a decision,
  not a refactor."*
- **Item 5's collision rule** deliberately prints a full surname, which the plan's contract forbids
  outright. I am arguing the contract is wrong in that one case: an ambiguous name on a pickup wall is a
  safeguarding failure, and the rule that produced it was written to prevent one.
