# Team points, wall displays and the end of night reveal

Three screens share one set of scores:

| Screen | Route | Auth | What it is |
| --- | --- | --- | --- |
| Game Scoring | `/Games/Points` | authorized | Leaders tap tiles to award points |
| Totals + reveal remote | `/Games/Scores` | authorized | Tonight's tally, and every control for the reveal |
| Live wall board | `/Display/Scores` | **anonymous** | Standings on a screen, updating as points land |
| Reveal | `/Display/Reveal` | **anonymous** | End of night ceremony, driven from `/Games/Scores` |

## Why the display pages are anonymous

A screen on a wall cannot sign in. Both display pages carry `[AllowAnonymous]` and read
through `IGameDisplayService`, which is routed under `public/` rather than `gRPC/` because
the reverse proxy demands a cookie for everything under `gRPC/`. **Only ever put aggregate
scores through that service** - no people, no service detail beyond a title.

## The displays take no input

Signage has nobody standing at it. `/Display/Reveal` has no buttons, no keyboard handling
and no click targets. Anything the operator needs to drive lives on `/Games/Scores`.

State travels phone → server → wall through `GameBoard.RevealStep` (nullable int, null =
no reveal running), which rides the existing `WatchScoreboard` stream. That is the only
thing both ends share, which is why it lives on the board rather than in memory.

## Reveal step model

`GameReveal` builds the running order and **both ends must build it identically** - the
phone knows only a step number, the wall has to turn that number back into "game three".
The display clamps anything it cannot make sense of.

```
intro → one step per round → one step per podium placing (counted down, 1st last)
```

Rules that were arrived at the hard way:

- **Every game played gets a round, even a scoreless one.** Skipping empty games meant a
  night of five games opened on "Game 2" and counted four. A scoreless round gets the
  `NO POINTS!` stamp instead of a count.
- **Behaviour points get a round only if any were awarded** - unlike a game, nobody
  "played" it, so its absence is not a hole in the count.
- **Podium steps come from the placings that exist, not from 3-2-1.** Three teams tied for
  first means one podium step; stepping through an empty "revealing 2nd" is dead air.
- **The podium holds every team in placings 1-3, however many that is.** Taking the top
  three *teams* drops the fourth of four tied for first. Ten tied for first shows ten
  plinths (they shrink via `flex: 0 1 26vmin`).

## Ties

Ties are rare, which is exactly why they must be unmissable - a child should never have to
work out who won.

- **Competition ranking everywhere**: `place = count(teams strictly ahead) + 1`. Two on the
  same score are both 2nd and the next is 4th.
- **Every team on the top score gets the crown / leader highlight**, not just the first row.
- **Say it in words**: podium title reads "It's a tie - 3 winners!", plinths read
  "TIED 1ST". Two gold medals alone do not explain themselves.
- **Ordinals, not shorthand**: "1st", "7th". `=1` is athletics convention and confused the
  first adult who saw it. `GameReveal.Ordinal` handles the 11th-13th trap.
- Rank (where a row sits, always unique) and place (what it is called, shared) are separate
  values. Do not merge them - rank drives the slide animation and must not collide.

## No commentary text on the displays

The reveal used to narrate ("RED TAKE THE LEAD!", "No points in this one!"). That was
removed deliberately. Round titles, placings, names, totals and podium wording stay;
generated colour commentary does not. A round that needs explaining gets a *visual* (see
the stamp), not a sentence.

## Animation rules

**Rows are positioned by rank, not by document order.** Every board keeps a stable DOM
order (keyed by team index) and moves rows with `transform: translateY(var(--rank) * …)`.
Reordering the markup instead makes an overtake teleport, and watching a team climb past
another is the entire point of the reveal.

**The count-up drives the ranks.** During a round every team walks from its previous total
to its new one at *one shared pace* (`biggest gain / duration`, duration clamped 1.2-5s),
so the smallest earner lands first and the biggest is still climbing. Ranks and bar widths
read the in-flight numbers, so overtakes happen live. Consequences:

- **A bar is measured against the leader's total once the round has landed, not against the
  running leader.** Measuring against the live maximum made every bar shrink as the biggest
  number climbed - a team that had just won nothing watched its bar get shorter, and to a
  child that reads as points being taken away. The board takes the room the round needs when
  the round's *name card* appears, before anything is counting, so during the count bars only
  ever grow. (The crown still follows the live leader; that is a different question.)
- Bar `width` transition must be short (~.12s). A 1.1s transition trails its own number.
- Row `transform` transition ~.5s - long enough to watch, short enough to keep up with a
  team passing two others in a second.
- The ticker must **not** restart on stream keepalives. The scoreboard re-sends the same
  board every 30s; restarting on that springs the numbers back to the start mid-count.
  Guard on the step actually changing (`_countedStep`).

**Striped bars: move the layer, not the background.** A CSS gradient tiles at the element
box and the pattern restarts at every tile edge, so animating `background-position` drags a
seam across the bar - visible near the right end of a full width bar and constantly on a
short one. Instead the stripe layer overhangs the bar (`left/right: -8vmin`, clipped) and
animates `transform: translateX()` by exactly one pattern period. For a
`repeating-linear-gradient(115deg, … P)` the horizontal period is `P / sin(115deg)`; at
`P = 3.4vmin` that is `3.75vmin`. Land on a whole number of those or the loop point snaps.

**One step per round, card and scores combined.** The round name lands big and centred over
a dimmed board showing the *previous* standings, holds 3s (`CardHold`), then flies up into
the header as the count starts. C# gates the un-dim and the count; the card's own keyframe
handles the movement. Both start on the same render, so keep the percentages in
`reveal-card-fly` in step with `CardHold` if either changes.

## Display multipliers

Scoring stays in ones - a leader taps once for one point, and the records hold ones. The
**screens** multiply, so one tap reads as 1000. `GameMultipliers` owns the whole idea.

- **Per game, defaulting to the night.** `GameBoard.PointsMultiplier` (default 1000) is
  the night's rate; `GameDefinition.Multiplier` overrides one game, for the game that
  scores twenty times as often as the others.
- **A game with no multiplier follows the game before it**, resolved by walking backwards
  (`GameMultipliers.For`). That is what makes a new game inherit *without anything being
  written when it starts* - so a game started offline on two phones cannot disagree, and
  there is no "new game" hook to forget.
- **Behaviour points have a multiplier of their own** (`GameBoard.BehaviourPointsMultiplier`,
  also defaulting to 1000). They are handed out all evening for something other than winning
  a game, so a game dropped to 100 must not quietly re-price them - and a night may want
  them worth more or less than a game is worth either way.
- **Multiply per game, then sum.** A night with two multipliers must total to what the
  board showed round by round, so `Total` is the sum of already-multiplied games.
- **The multiplying happens once, server side**, in `GameDisplayService`. Everything in
  `GameScoreboardResponse` is already multiplied, so the display screens never see a raw
  point and cannot apply it twice. `/Games/Scores` does its own multiplying because it
  reads the offline store rather than the stream - it is the one place both numbers exist.
- A game entry is sparse (see below), so **a multiplier is a reason to keep one**. Anything
  that filters `Games` on "has a name" will silently drop overrides - `WithGame`,
  `NormaliseGames` and the team-count reset all have to say so.

Consequences worth knowing:

- **Placings come off the multiplied totals everywhere**, including while `/Games/Scores`
  is toggled to scored points. Different multipliers can rank two teams differently, and
  the wall is the one the room saw. It also keeps the reveal's step count identical on
  both ends - one end counting a tie the other does not slides every later step out.
- **The reveal times a round in scored points but counts in display points.** How long the
  round runs comes from its granularity - the highest common factor of its gains, since the
  stream only carries multiplied numbers - so a five tap round lasts the same at 1x and at
  1000x instead of pinning itself to the 6s ceiling. The numbers themselves still climb
  through every value in between: quantising the climb to whole taps made a 1000x round
  land in five visible jumps, which reads as a slideshow rather than a scoreboard.
  `PointsPerSecond` is *taps* per second (4) with a 2.5s floor - the count up is the round's
  moment, and a board that snaps to its answer is over before a child finds their team.
- Screen numbers are grouped (`GameScoreFormat`) - "15,000" rather than counting noughts
  off a wall.

## Placement scoring

A race is scored by finishing order, not by tapping: one tap per team, then one award for
the whole heat. `GamePlacements` owns what a place is worth; `GamePlacementOrder` owns the
order being built.

- **The values on `GameDefinition.PlacementPoints` are the mode.** Non-null means the game
  is a race; there is no second flag to keep in step. It is in the list of things
  `WithGame` and `NormaliseGames` count as worth keeping, and in the team-count reset's
  filter - miss one and a placement game with no name vanishes on sync.
- **Placement does not inherit forward, unlike the multiplier.** A multiplier is a rate; a
  way of playing is not. Game 4 quietly becoming a race because game 3 was one is the
  confusion the feature exists to remove.
- **Values are scored points**, like everything a leader touches - 10, not 10,000. The
  wall multiplies as usual.
- **Ties: everyone on the place gets its full value and the next place is skipped.**
  Competition ranking, the same rule as the board and the reveal. Not an average - a
  number on the wall that matches no placing is unreadable, and two teams on 950 with
  nobody on 1st is worse.
- **Combined sides place as one side**, so their teams land on the same place and read as
  tied on the totals page. That is what happened - they crossed the line together - so it
  is not a case to special case away.
- **A place worth nothing is still a place**, so it gets a record. That is why
  `GamePointRecord.Place` is stored rather than inferred from the points, and why
  `GamePointRecordService.Create` lets a zero through when a place is set: otherwise
  "came fourth when only three score" and "did not run" are the same row.

### Rounds

Heats of one game **all carry that game's number**. Nothing about the tally, the display
or the reveal changes - game 3 is still one round of the reveal with one total.

A round is not stored as a thing of its own. It is the records of one award read back
together, keyed by the `GroupId` they share, which is what undo already worked on. Round
numbers are worked out by ordering those groups - a label, nothing computes off it. Two
phones scoring the same game offline would both call theirs "round 2", so a stored number
would be wrong on merge in a way a derived one is not.

Rewriting a round (the totals page) deletes its records and writes new ones under the same
group id and the same awarded time, so an edited heat keeps its place in the order.

Taking a record back is where the duplicates come from, and there are **three** rules
holding it together. All three exist because a team scored twice on the wall is the one
mistake a night cannot recover from.

1. **`MergeRecordsFromServer` keeps a local tombstone while its delete is still queued.**
   The server still has the record as live, and a refresh landing in that window puts the
   points straight back.
2. **A record being taken back is always tombstoned and always queued for deletion, even
   when its create is still sitting in the outbox.** A flush already running holds its own
   snapshot of that queue, so the create may be on the wire right now; treating "still
   queued" as "the server never saw it" leaves it to land with nothing ever deleting it.
   The cost when the create really had not gone is one delete the server answers "not
   found" to, which the queue drops.
3. **After a create is accepted, the flush re-checks whether that record has since been
   taken back**, and queues the delete itself if so. That is what closes the window rather
   than narrowing it.

Re-pricing is the stress test: one press of a place's `+` rewrites *every* heat in the
game, back to back, against a flush that is already in flight. Eight presses in a row must
leave exactly one live record per team per round.

Changing what a place pays re-prices the heats already scored in that game. A leader who
decides the race should have been worth more means the round they just watched.

### Where each half lives

`/Games/Points` does the cheap gestures only: tap to place, `= 1st` to tie with the last
place given out, one button to award, undo. `/Games/Scores` is where a placing, a score or
a whole round is changed after the fact - it is used on a desktop, so the round editor is
built around a table of teams against rounds rather than a phone list.

## Planning a night ahead, and voiding a game

A big night is laid out in the hall before anybody reaches the field. Two flags on
`GameDefinition` carry it, and **one rule decides everything**:
`CountsTowardNight() = !Planned && !Hidden`.

- **`Planned`** - added ahead of time and not part of the night yet. Clears itself the
  moment the game is opened or scored (`GoToGameNumber`, `EnsureCurrentGamePlayed`), so
  there is no "start it" step to forget. `New game` picks up the lowest planned game after
  the current one rather than opening a blank one, which is what makes planning pay off.
- **`Hidden`** - voided. Out of the tally, off the wall, no round in the reveal, and its
  points stop counting. Its records are left alone, so un-hiding restores the game exactly;
  hiding must never be a destructive act wearing a display setting's clothes. Never clears
  itself - it was somebody's decision.

A game that counts for nothing must have **no column**, not a column of zeroes: otherwise
the row stops adding up to the total, and a child comparing them will notice before you do.

**The filter is applied twice and must match.** `GameBoard.CountingGames(gamesPlayed)` on
the phone, and the same test over `DbGame` in `GameDisplayService`. Everything downstream is
positional over that list - `GameNames`, `PerGamePoints`, the tally's columns, the reveal's
running order - so one end filtering where the other does not slides every later reveal step
out of place. Two consequences already paid for:

- `CurrentGamePoints` is summed from the records, **not** read as `perGamePoints[currentGame - 1]`.
  Once the list holds only counting games, its positions are not game numbers.
- `GamesPlayed` on the response is the *count of counting games*, not the highest number
  reached.

Deleting a game clears its records and drops its definition, but **the numbers of later
games are left alone** - every record names its game, so renumbering would move a phone's
queued taps into the wrong game. A game deleted from the middle is hidden as well, or the
night reaching past it would bring it back as an empty column.

Correcting a tapped game writes the **difference** (`SetGamePointsAsync`), so a laptop
correction merges with a phone still scoring instead of fighting it. Placement games are not
corrected that way - their rounds are the record of what happened, and an invisible
adjustment beside them would stop the heats adding up to the game.

## Where things live on the totals page

Two tabs, because the page does two jobs: **Scores** (the tally, and the reveal remote) and
**Set up** (`GameSetup`). Tabs rather than another chip pair - the chips below already mean
"how should this number read", and two rows of chips meaning different things is how a page
stops being readable.

`GameSetup` exists because a game used to be edited in two panels at once - name and
multiplier in one, placings and heats in another - and neither could add a game, void one,
or fix a score that went in wrong. **A game is one thing, so it gets one card**, holding its
name, multiplier, scoring mode, placement values, heats and scores. Everything in that
component is in scored points; the tally beside it is where screen numbers are checked.

When markup moves between components, its **scoped CSS has to move with it**. The
`b-<hash>` attribute is stamped per component, so a rule left behind matches nothing and
fails silently - the elements are all still there, just unstyled.

Three ways a style silently does nothing here, all of which cost time:

- **`--mud-palette-background-grey` does not exist** - MudBlazor spells it `gray`. An
  invalid variable makes the declaration a no-op, so a card simply has no background. That
  is what left the coloured edge on each score row standing on nothing, reading as a stray
  tick hanging off the row before it.
- **Elevation alone separates nothing in this theme.** MudBlazor's shadows are black at low
  alpha and the background is near black, so `Elevation="4"` on a card whose surface matches
  its parent is invisible. `MudPaper` defaults to the same surface as its parent, so
  nesting one in another needs an explicit colour. Depth reads as *lighter*: the set up page
  goes recessed section → raised card → inset row, with elevation on top of the surface
  shift rather than instead of it. Both were tried in the browser and compared.
- **A class on a MudBlazor component gets no scope attribute**, so `.my-class ::deep input`
  never matches when `.my-class` sits on the `MudTextField` itself. Put it on your own
  wrapper element and reach in from there.

## Scoring data

Points are **append only deltas** (`GamePointRecord`), so two phones scoring the same game
offline merge rather than overwrite. Board settings are config and use last-write-wins on
`UpdatedAt`. Anything added to `GameBoard` must be threaded through **four** places or it
silently vanishes on sync:

1. `GameBoard` (contract) and its `Default`
2. `UpsertGameBoardRequest`
3. `GamePointsService.UpdateBoardAsync`'s outbox projection
4. `DbGameBoard` + `Create.cs` upsert (both the insert and the update branch), plus a
   migration

A field on `GameDefinition` needs the same treatment minus the migration - `DbGame` is a
JSON column - plus a line in `NormaliseGames`, which is also what decides whether a game
entry is worth keeping at all.

A field on `GamePointRecord` needs `CreateGamePointRecordRequest`,
`GamePointRecordServices/Create` (which is also where the record is validated) and
`DbGamePointRecord` plus a migration. The service's own outbox writes both the record and
the request side by side, so a field added to one and not the other survives locally and
is lost on sync.

`GameDisplayService.Signature()` is what decides whether the stream bothers pushing. A new
field the display renders **must** be added there or the wall will never see it change.
Values that are multiplied before they are sent cover themselves; a multiplier changed on
a board where nothing has been scored moves no number and pushes nothing, which is fine
because there is nothing on the wall to correct.

## Testing this locally

- The stream only wakes on a change event from the API. A direct SQL write is picked up on
  the 15s `WatchTick`, so seed then wait ~17s. Driving `/Games/Scores` instead pushes
  immediately.
- The browser pane throttles timers when hidden, so JS sampling of a running animation
  times out. Screenshot bursts work; a still caught early in an animation looks broken
  (medals render as dots mid `scale(0)`) - take a second one before believing it.
- Seed script for a reveal with lead changes and a tie: see the session scratchpad, or
  insert `GamePointRecords` rows directly (`TeamIndex`, `Points`, `GameNumber` null =
  behaviour).
