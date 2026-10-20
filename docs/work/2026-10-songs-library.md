---
title: Song library — songs copied out of ProPresenter for song leaders
kind: plan
status: in-progress
module: propresenter
opened: 2026-10-20
verified: 2026-10-20
code:
  - GSBC.ImpactKids.ProPresenterAgent/Songs/SongSync.cs
  - GSBC.ImpactKids.ProPresenterAgent/ProPresenter/ProPresenterApi.cs
  - GSBC.ImpactKids.Grpc/Features/ProPresenter/ProPresenterAgentServices/SyncSongs.cs
  - GSBC.ImpactKids.Grpc/Features/Songs
  - GSBC.ImpactKids.Grpc/Data/Models/Songs
  - GSBC.ImpactKids.Shared.Contracts/Entities/Features/Songs
  - GSBC.ImpactKids.Shared.Contracts/Entities/Features/ProPresenter/ProPresenterLibrary.cs
  - GSBC.ImpactKids.WASM/Features/Songs
  - GSBC.ImpactKids.WASM/Features/ProPresenter/Pages/Machines.razor
  - GSBC.ImpactKids.WASM/Features/Scheduling/Features/Schedules/Components/ScheduleItemDialog.razor
---

# Song library — songs copied out of ProPresenter for song leaders

The ProPresenter agent copies the songs in chosen ProPresenter libraries into Impact Kids, where `/Songs`
lists them with their words for song leaders to read and choose from. Read this before changing the
sync or reading anything else out of ProPresenter: it records what ProPresenter 22 keeps on disk, why
the agent reads the network API instead, and the rule that stops a half-read library deleting songs.

**Where it stands (2026-10-20).** The first slice is built and seen working end to end against the real
ProPresenter on the Impact Kids Mac: libraries reported and chosen (several at once) on the machines
page, 26 songs synced, search by words, lyrics by arrangement, and a library unticked soft-removing its
songs. Not yet released, so the installed agent on the Mac does not sync songs until the next release.
Not checked at phone width.

## The rule that matters: only a complete list is ever sent

`SyncSongs` treats a song this machine sent before and has not sent now as **deleted in ProPresenter**,
and marks it removed. So the agent (`SongSync.ReadAsync`) sends nothing at all if any library or
presentation fails to read — ProPresenter closed mid-read, one call timing out — rather than the songs
it did get. A partial send would empty the library on every phone.

Two cases deliberately *are* sent, and do remove songs:

| Case | Why it is the truth |
|---|---|
| A chosen library is no longer in ProPresenter's library list | Deleted or renamed there; its songs are gone too |
| A leader unticks a library | Its songs should leave the song library |

Removal is soft (`DbSong.RemovedAt`), and scoped to the machine that sent the song
(`SyncedByMachineId`), so a second Mac copying other libraries never deletes the first one's. Ticking
a library again brings back the same rows and ids.

**The API sees only ProPresenter's active workspace.** Switching ProPresenter to a workspace without
these songs will remove them here within five minutes, and switching back restores them. Today both
workspaces on the Mac hold the same songs (below), so this has not bitten.

## What ProPresenter 22 keeps on disk, and why the agent does not read it

Found on 2026-10-20 on the Impact Kids Mac (ProPresenter 22, workspace schema 22.7.0).

- Workspaces are listed in
  `~/Library/Application Support/RenewedVision/ProPresenter/Workspaces/registry-v1.sqlite3` (`workspaces`
  table; `meta.active_local_uuid` is the open one). There are two: **Impact Kids**, linked to Renewed
  Vision's cloud (organisation 120091), and **Impact Kids - Local**, a copy of it made on 2026-10-19 22:51
  (its `DocumentStore/import-manifest.json` names a `WorkspaceDuplicate` source).
- Each workspace is one SQLite file, `<uuid>/DocumentStore/workspace-v1.sqlite3`. A presentation is a
  row of `workspace_streams` with `kind = 'Presentation'` and `core_path` like `Songs/Deep and Wide` — the
  library is the first path segment. The `.pro` path the API reports does not exist.
- The row's `snapshot` is a **Loro CRDT document** (header `loro`), shaped like the old `rv.data`
  protobuf; slide text is RTF; group order is a Loro movable tree, `cue_group_placements`.
- The two workspaces held semantically identical songs on 2026-10-20 — same 26 ids, words, groups and
  arrangements — but every row's `sha256` differs, because each copy carries its own edit history.
- No song had any CCLI detail filled in, so there is nothing of that to show.

The network API (`/v1/libraries`, `/v1/library/{id}`, `/v1/presentation/{id}`) returns the same songs as
plain text, documented, and is already what the agent talks to. Reading the files would reach the
inactive workspace too, but means decoding an undocumented format that is still migrating, with a
native Loro library .NET does not have. Not worth it while both workspaces hold the same songs.

## What the API gives, and the quirks the agent handles

Proven against all 26 songs on 2026-10-20.

| Quirk | Handling |
|---|---|
| A library item's name can carry the arrangement — `Father Abraham - [ Default ]` | The name comes from `/v1/presentation`, never from the library listing |
| Every presentation lists an arrangement "Master" whose uuid is the presentation's own | Dropped; it is ProPresenter's made-up "every group once", which the page shows as *Every part once* |
| `current_arrangement` is the Master uuid on most songs | Stored as no default arrangement; the page still opens on a real one (below) |
| Arrangements made for a night, like `21/11/25` | Kept; a song leader may want last term's order |

A group's colour arrives as 0–1 channels and is stored as `#RRGGBB`; blank slides are dropped.

**A song opens on an arrangement whenever it has one** (`SongLyrics.OpeningArrangement`), because an
arrangement is the order a song is sung in and every part once is not: ProPresenter's own choice, else
the one called "Default", else the first. *Every part once* is the opening view only for a song with no
arrangements, and is otherwise the last chip.

## The library choice: null is not empty

`DbProPresenterMachine.SongLibraryUuidsJson` is **null until a leader saves the machine**, and null means
"the library called Songs" (`ProPresenterLibrary.SongLibraries`, in Contracts so the admin page and the
agent agree). Once saved it is never null again, and `[]` copies nothing. So a newly connected Mac
starts copying its Songs library without anyone choosing it.

A newly connected Mac's first tick has reported no libraries yet, so the default resolves to nothing
and that first sync sends an empty list — harmless, since the machine has no songs to remove — and the
next tick, 30 seconds later, copies the Songs library.

## Load

The agent reads ProPresenter every five minutes, or at once when the chosen libraries change, and sends
only when the songs' hash differs from the last send (or an hour has passed, so a restored database
is put right). The server rewrites a song only when its own hash of name, library and content differs,
and raises the `Song` change event only when something changed — the same reason `ReportStatus` does
not raise one per heartbeat. Bounds per sync are in `SyncSongs.cs` (5000 songs and so on).

The whole library travels to the phone in one `BasicReadMultiple`, words included, so the page can search
words without a server call: about 30 KB for 26 songs.

## Verification, 2026-10-20

Dev agent against the local AppHost and the real ProPresenter on the Impact Kids Mac (the installed
agent was stopped for the test and restarted after, because both bind the feeds port 51080):

- `LibrariesJson` reported all seven ProPresenter libraries; the machines page showed *Songs from: Songs*
  before anyone had chosen.
- 26 rows in `Songs`, names without arrangement suffixes.
- `/Songs`: search "fountain flowing" found *Deep and Wide* and showed that line; "hes able" and
  "he's able" both found *He’s able*.
- *Father Abraham* opened on ProPresenter's *Default* arrangement, chorus repeated between verses;
  *Every part once* showed each group once. Opened from the list: *The Lord's Army* (no choice in
  ProPresenter, no "Default") on *Gazelle*, its first; *Deep and Wide* on *Default*; *My Bible I Read
  Before Breakfast* (no arrangements) on every part once, with no chips.
- Ticking *Segments* on the machines page synced its one presentation and showed library chips; unticking
  it set `RemovedAt` on that row. No song row was rewritten by the repeated unchanged syncs.

## Songs in schedules

Built 2026-10-20. An item template can be marked as a song (`DbScheduleItemTemplate.IsSong`, "A song,
chosen from the song library on each service" on `/Schedules/Items`). A song is always a placeholder — the
server sets `NeedsFillIn` with it and refuses it on a section — so the shape is a *Songs* section with
*Song 1*, *Song 2* … under it. The existing *Song 1/2/3* rows on 2026-10-20 schedules are plain one-offs and
do not get the picker; templates have to be rebuilt from a song item to get it.

On a song item the fill-in is a song field: it suggests library songs by name and still takes any typed
name, because a brand-new song is often sung before anyone has entered it in ProPresenter.

| Typed / chosen | Stored |
|---|---|
| A library song's name (any case) | `SongId`, and `FillIn` set to the library's spelling by the server |
| Anything else | `FillIn` only — "Not in the song library yet" |

`ArrangementUuid` is optional; null means the song's usual one, and the lyrics page opens on it via
`?arrangement=`. The server checks the arrangement belongs to the song. Copying a schedule copies the link.

**Typing the name of a song added to ProPresenter later does not link it backwards.** An item saved by
name stays unlinked until someone opens it and picks the song.

**Last sung / how often** (`Song.LastSungAt`, `TimesSung`) are worked out on every read of the library
(`SongService.SungAsync`), from linked items in *service* schedules — templates do not count — on
services up to the end of today in church time. A song twice in one night counts once. The song store
refreshes on song events, not schedule ones, so a page open while a schedule is edited shows the old
count until it next loads.

Seen working on 2026-10-20 (desktop width only): a *Song 1* template made with the flag; Father Abraham
picked by search with its *Default* arrangement, stored with both ids; *Brand New Song* typed and stored by
name; *Words* opening the lyrics on the chosen arrangement; Father Abraham on 19/10 counting as
"19/10/2026 · once" and the 21/10 one not counting.

## Not built

- **Putting songs into ProPresenter.** The network API has no route that creates or imports a
  presentation. ProPresenter 22 does contain a client for *content partners*, found by
  `/.well-known/renewed-vision-content-partner` (only `partner.api.joinamazing.com` answers it,
  2026-10-20), which would let ProPresenter browse and import from Impact Kids natively — but it looks
  like an approved-partner list; that is an inference from strings in `ProCore`, not confirmed.
- Building a night's playlist from chosen songs, which the API should allow (`/v1/playlist`); untried.
