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
