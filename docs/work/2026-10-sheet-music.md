---
title: Sheet music — songs' music kept as data, read from a scan or a photo
kind: discussion
status: proposed
module: propresenter
opened: 2026-10-20
verified: 2026-10-20
code:
  - GSBC.ImpactKids.Grpc/Data/Models/Songs
  - GSBC.ImpactKids.WASM/Features/Songs
---

# Sheet music — songs' music kept as data, read from a scan or a photo

What was agreed on 2026-10-20 about keeping sheet music against the songs in the song library, so a
pianist's copy can be reprinted. Not started: it comes after songs can be put into schedules. Read this
before designing the slice.

Nothing here goes to ProPresenter.

## What was decided

- **The music is kept as data, not as PDFs or images.** The point is to reprint it, and to transpose it,
  which a picture cannot do.
- **Two formats, both kept:**

  | Format | For | Why |
  |---|---|---|
  | MusicXML | a full score or piano part | the exchange format every notation program reads and writes |
  | ChordPro | a lead sheet or chord chart — words with chords | plain text, trivial to transpose, and what many pianists play from |

- **It lives in its own table, never in `DbSong`.** `DbSong` belongs to the ProPresenter sync and is
  rewritten whole when ProPresenter's copy changes (`DbSong.ContentJson`, `ContentHash`), so anything a
  leader added there would be lost. Roughly `DbSongSheet`: song, kind (score / lead sheet / chord
  chart), key, the MusicXML or ChordPro, where it came from, when it changed. A song can have several.
  A soft-removed song keeps its sheets.
- **Reading a scan is the first slice, not a later one.** Leaders will not run a notation program
  themselves; without the scan step the feature is unused. Assume the input is:
  1. a PDF of the music (the best case), or
  2. **a phone photo of a printed sheet — expected to be the usual case.**

## Reading the music: run two OMR engines side by side

Optical music recognition (OMR) never comes back clean — wrong notes, dropped ties, lyric syllables
under the wrong note, worse from a photo. The idea is to run two engines on the same page and let the
leader choose, per line (system), whichever came back closer — in the hope that the two make different
mistakes.

| Engine | Strength | Notes to check before relying on it |
|---|---|---|
| [Audiveris](https://github.com/Audiveris/audiveris) | the established open-source OMR; good on clean printed scores and PDFs | Java; AGPL — run unmodified as its own container, not linked in |
| [oemer](https://github.com/BreezeWhite/oemer) | deep-learning, made for phone photos | Python; slower, and less accurate overall |

Both write MusicXML, so the server would run them as containers beside the gRPC service (Aspire
locally) and keep both results until the leader has chosen.

Open, to settle in the slice:

- **Merging by line.** Choosing per system needs the two results aligned system by system and measure
  by measure. They will not always agree on how many measures a line has. Unknown how often that
  happens on real kids' song sheets — try both engines on a handful of the church's actual sheets,
  photographed on a phone, before designing the merge.
- **ChordPro from a scan.** Neither engine produces ChordPro. A lead sheet's chords would come from the
  MusicXML's chord symbols (`<harmony>`), if the engine read them.
- **Whether an editor is needed.** Choosing between two readings may still leave mistakes that neither
  got right. Not decided; a full notation editor in Blazor is a large job, and the answer depends on
  how good the merged result is in the trial above.

## Showing and printing

Render MusicXML in the browser with [Verovio](https://www.verovio.org/) (WASM, draws SVG, can
transpose), or OpenSheetMusicDisplay. Verovio is preferred for transposition. Printing is that SVG with
a print stylesheet. ChordPro is rendered as text.

## Copyright

Reprinting licensed music is normally covered by the church's CCLI copy licence, and copies are
reported. Check before this becomes the way music is reprinted.
