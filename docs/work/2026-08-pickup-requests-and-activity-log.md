---
title: Pickup requests, the activity log, and the pickup wall display
kind: plan
status: in-progress
verified: 2026-08-27
code:
  - GSBC.ImpactKids.Shared.Contracts/Entities/Features/Attendance
  - GSBC.ImpactKids.Grpc/Features/Attendance
  - GSBC.ImpactKids.WASM/Features/Attendance
---

# Pickup requests, the activity log, and the pickup wall display

## The problem, as it happens on the night

Children are held in one room. Parents come to the sign-out desk one at a time, the child is
signed out, and their name is then called into the room. While that name is being called the
desk is already signing out the next child. A child who has been called takes another three
minutes to find their bag, so by the time they reach the door the person on the desk can no
longer remember whether *that* child was signed out or is only being fetched.

Two facts are being conflated today, and the record only holds one of them:

| Fact | Who knows it | Stored today |
| --- | --- | --- |
| A parent has arrived and asked for this child | the sign-out desk | nothing |
| The child has actually left with them | the door | `SignedOut` |

## The shape of the answer

**`Requested` is a third state, and it is optional.** One button press, no flow, no
confirmation. A child may go straight to signed out without ever being requested — the
request is a convenience for the room, never a gate in front of the sign-out.

```
        ┌──────────────┐   quick press   ┌───────────┐   full flow   ┌────────────┐
        │  Signed in   │ ──────────────► │ Requested │ ────────────► │ Signed out │
        └──────────────┘                 └───────────┘               └────────────┘
                │                                                          ▲
                └──────────────────── full flow ───────────────────────────┘
```

A request can be taken back, so the **operation** is a toggle: `RequestPickup` carries a
`Requested` bool and the server is idempotent either way.

The **control** is not. Requesting is the big tap target; once requested, that target becomes
inert and informative ("Requested 3m ago · 7:41pm · Sam") and clearing moves to a small,
separate ✕ beside it.

That split is not fussiness — it is the two-leader case the whole feature exists for:

1. Leader A taps Jonah. A's row reads "requested".
2. The event reaches B's phone and B's row updates too — so on a single toggling target, B's
   button is now a **clear** button where a **request** button used to be.
3. B, looking at the queue rather than the screen, taps Jonah because Jonah's parent is
   standing in front of *them*. The request is cleared, Jonah's name leaves the wall, and
   nobody at the desk knows.

The same shape happens on one phone: a leader who taps, gets distracted, comes back and taps
again "to make sure" un-does it. **A control designed to be tapped without thinking must not
have a destructive second meaning on the same pixel.**

The trade is deliberate. A genuine mis-request now takes a smaller, more careful target to
undo. That costs three seconds; an accidental clear costs a child nobody is fetching.

## The three pieces

### A. An activity log on the attendance page

Reverse-chronological list of what has happened tonight, on `/Attendance/Tool`. No new
table: an `AttendanceRecord` already carries up to three timestamped events, so the log is
built by fanning each record out into its events and sorting them.

```
7:42pm  Ella M.      signed out    by Sam
7:41pm  Jonah P.     requested     by Sam
7:12pm  Ella M.      signed in     by Priya
```

A log the desk can glance at answers "did I already do that one?" without a search.

### B. A pickup wall display

The room's TV runs ProPresenter and already shows `/Display/Scores`. It gets
`/Display/Pickup`: the children currently **requested and not yet signed out**, largest
first-come first, so the room can send them to the door. A child leaves the wall the moment
they are signed out.

### C. A "Requested" filter

So the desk can sweep for anyone requested who never actually got signed out — the failure
mode this feature introduces, and therefore the one it has to answer for.

## The privacy decision, stated plainly

> **Superseded on 28 Aug 2026 — read this first.** The rules below described a purpose-built
> display service that returned a name and a time and nothing else. **That service no longer
> exists.** The wall now reads the ordinary attendance, people and service endpoints and works
> out who is waiting client side, so a display can read everything a `Person` carries — date
> of birth, allergies, medical notes, family.
>
> That was a deliberate trade, made by the owner: the bespoke contract, service and stream
> were a few hundred lines existing only to narrow a response, and the control that actually
> matters is the enrolment key, which is his and lives on screens he controls. What replaced
> the narrowing is a **hard read-only guarantee** — a display cannot write anything, enforced
> both by the per-method policies and by an EF interceptor that refuses `SaveChanges` for a
> display caller. See `docs/modules/auth/sign-in.md`.
>
> **Read-only is about integrity, not confidentiality.** It stops a screen changing anything;
> it does not narrow what a screen can see. The paragraphs below are kept because the
> *reasoning* about what is legible from the third row is unchanged and still governs what the
> page renders — but they no longer describe the transport.

`docs/modules/games/README.md` says of the games display service: *"Only ever put aggregate
scores through that service — no people, no service detail beyond a title."* That rule stands
and nothing person-shaped was ever added to `IGameDisplayService`.

What the **page** puts on a wall is still the narrow thing, and this part is unchanged:

- It renders **a display name only** — full first and last name ("Jonah Parry"). No date of
  birth, no family, no medical or allergy detail, no ids.

  > Widened on 28 Aug 2026. This was first name plus last initial ("Jonah P."), and the
  > paragraph below already said that changing it is a decision rather than a refactor — so,
  > recorded as one. The rest of the list is unchanged: the extra field is the surname and
  > nothing else.
- It renders **only children currently requested and not yet signed out** for one service —
  never the roster, never who is signed in, never a history.
- The screen is in a room full of the parents of those children, which is the only reason
  a name on a wall is acceptable at all. It is not a general-purpose reason.

That set of rules is the contract **for what the page draws**. Widening it is a decision, not
a refactor. What the transport permits is now wider than what the page draws, and the note at
the top of this section is where that was decided.

## The data

Additive only — two nullable columns on `DbAttendanceRecord`, mirrored on the contract:

| Column | Type | Meaning |
| --- | --- | --- |
| `PickupRequested` | `DateTimeOffset?` | when a parent asked for this child; null = not requested |
| `PickupRequestedUserId` | `Guid?` | who took the request, for the log |

Nullable, no backfill, no rewrite of existing rows. Every existing record reads as "never
requested", which is exactly true.

`PickupRequested` is deliberately **not** cleared on sign-out. It is evidence of what
happened, in the same spirit as the soft `Deleted` flag — "signed out after being requested"
and "signed out cold" are different nights, and the log wants to be able to tell them apart.
Everything that asks "is this child on the wall" tests
`PickupRequested != null && SignedOut == null`, never `PickupRequested != null` alone.

## Ownership of the work

Four stations, disjoint file sets, one branch (`feature/pickup-requests-and-activity-log`).
The running-app gate in [AGENTS.md](../../AGENTS.md) is satisfied by the orchestrator after
integration, not per station — the app is a single Rider run configuration and a single
database, so stations build (`mcp__rider__build_solution`) but do not run.

| Station | Owns |
| --- | --- |
| Ideation | nothing — reads and proposes |
| Backend | `Shared.Contracts/**/Attendance/**`, `Grpc/**/Attendance*/**`, `Grpc/Program.cs`, the migration |
| Attendance page | `WASM/Features/Attendance/Pages/Tool.*`, `WASM/Features/Attendance/Pages/Family.razor`, new components under `WASM/Features/Attendance/Components` |
| Wall display | `WASM/Features/Attendance/Pages/PickupDisplay.*` (new), `WASM/Program.cs` |

## The agreed surface

Fixed up front so four stations can build against it in parallel. The backend station owns
these files; everyone else codes against this table and does not edit them.

### Contract additions — `AttendanceRecord`

```csharp
public DateTime? PickupRequested       { get; init; }
public Guid?     PickupRequestedUserId { get; init; }

[ProtoIgnore]
public DateTime? LocalPickupRequested => PickupRequested?.ToLocalTime();

/// <summary>On the wall: asked for, and not yet gone.</summary>
[ProtoIgnore]
public bool AwaitingPickup => PickupRequested != null && SignedOut == null;
```

### Authorized service — `IAttendanceRecordService`

`IUpdateService<T>` is already spent on `SignOutAttendanceRecordRequest`, so the toggle is a
named method on the interface rather than another generic base, with its own operation file
(`AttendanceRecordServices/RequestPickup.cs`) per the partial idiom.

```csharp
Task<BasicResponse> RequestPickup(RequestPickupAttendanceRecordRequest request,
                                  CallContext context = default);
```

```csharp
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class RequestPickupAttendanceRecordRequest
{
    public required Guid Id { get; init; }

    /// <summary>false clears the request — the same button, pressed again.</summary>
    public required bool Requested { get; init; }
}
```

Server rules: writes `PickupRequested` and `PickupRequestedUserId` **by marked property**,
never `db.Update(entity)` — the desk and the door are two writers on one row (see
[GSBC.ImpactKids.Grpc/AGENTS.md](../../GSBC.ImpactKids.Grpc/AGENTS.md)). Ends with
`eventService.SendUpdatedEvent`, or the other phones never see it. Requesting a record that
is already signed out is a no-op success, not an error — it means someone tapped a stale
list, and an error on the desk mid-pickup helps nobody. Clearing sets both columns null.

### Anonymous display service

```csharp
[Service("public/GSBC.ImpactKids.Attendance.Display")]
public interface IAttendancePickupDisplayService
{
    Task<PickupDisplayResponse>                GetPickups(PickupDisplayRequest r, CallContext c = default);
    IAsyncEnumerable<PickupDisplayResponse>    WatchPickups(PickupDisplayRequest r, CallContext c = default);
}
```

```csharp
public class PickupDisplayRequest  { public Guid? ServiceId { get; init; } }   // null = today's service

public class PickupDisplayResponse : BasicResponse
{
    public string?                   ServiceTitle { get; init; }
    public List<PickupDisplayEntry>  Waiting      { get; init; } = [];
}

/// <summary>A display name and a time, and nothing else. No id — nothing here
/// may be turned back into a person.</summary>
public class PickupDisplayEntry
{
    public required string   Name        { get; init; }
    public required DateTime RequestedAt { get; init; }
}
```

`Waiting` is ordered by `RequestedAt` ascending — longest wait at the top, because that is
the child the room should chase. `WatchPickups` follows `GameDisplayService` exactly: claim
the change subscription *before* the read, a fresh `DbContext` per look via
`dbFactory.RunWithNewDbContext`, a 15s tick, a 30s keepalive, and a `Signature()` so an
unchanged board pushes nothing. A new field the wall renders must be added to `Signature()`
or the screen will never see it change.

### What the wall renders

`/Display/Pickup` and `/Display/Pickup/{ServiceId:guid}`, `[AllowAnonymous]`, `DisplayLayout`,
no input of any kind — the same rules as the score display. Empty state is a deliberate
screen ("Nobody waiting"), not a blank one; a wall that goes black reads as broken. Names
scale down as the list grows, the way `--rows`/`--scale` does on the score board.

## Backend station notes

Both slices build clean (`mcp__rider__build_solution`). Nothing in "The agreed surface" was
renamed. Four things worth knowing, one of which is a change the backend station could not
make.

### YARP needs a route, and it is not the backend station's file

`GSBC.ImpactKids.YARP/appsettings.json` matches the anonymous prefix **literally**, one route
per service:

```json
"public": {
  "Match": { "Path": "/public/GSBC.ImpactKids.Games.Display/{**catch-all}" }
}
```

There is no `/public/{**catch-all}` fallback, and the catch-all `wasm` route below it would
swallow `/public/GSBC.ImpactKids.Attendance.Display/...` and hand it to the Blazor app. So
**until a second route is added, the pickup wall gets HTML back instead of gRPC.** The
existing `grpc` route does not help — it carries `"AuthorizationPolicy": "Default"`, which is
the whole reason the games display is routed separately.

The addition, beside the existing `public` route:

```json
"pickup": {
  "_comment": "Anonymous on purpose - the pickup wall cannot sign in. Display name and time only.",
  "ClusterId": "grpc",
  "Match": { "Path": "/public/GSBC.ImpactKids.Attendance.Display/{**catch-all}" }
}
```

`appsettings.Development.json` and `appsettings.Production.json` carry no `ReverseProxy`
section, so this one file is the whole change. Left for whoever owns YARP.

### `PickupDisplayResponse : BasicResponse` needed a `[ProtoInclude]` on the base

The agreed surface derives the response from `BasicResponse`, and no other response in this
repo does that — they all restate `Success`/`Error` (see `BasicReadResponse<T>`). protobuf-net
only carries a base contract's members into a derived contract when the base declares the
subtype, so without a declaration the wall would have read every board as `Success = false`.

Implemented as written, plus one line on `Messages/Responses/Base/BasicResponse.cs`:

```csharp
[ProtoInclude(100, typeof(PickupDisplayResponse))]
```

Tag 100 is clear of the implicit fields (1, 2) and adding a subtype does not change the wire
format of a plain `BasicResponse`. This is the only edit outside the backend station's file
list in `Shared.Contracts`. **If the convention is preferred, the alternative is to stop
deriving and restate the two properties** — but that is a change to the agreed surface, so it
was not made.

### The notifier was generalised rather than copied

`GameDataChangeNotifier` was games-specific in name only — the mechanism is a swap-then-signal
`TaskCompletionSource`, with nothing game-shaped in it. It now lives in
`Grpc/Services/DataChangeNotifier.cs` as an abstract base plus `DataChangeSubscription`, and
both displays are one-line subclasses:

- `Features/Games/GameDisplayServices/GameDataChangeNotifier.cs` → `: DataChangeNotifier`
- `Features/Attendance/AttendancePickupDisplayServices/AttendanceDataChangeNotifier.cs`

Kept as **two** singletons rather than one shared instance, so a point scored during a game
does not wake every pickup wall and vice versa. Both are registered in `Grpc/Program.cs`.

Waking them is `Features/Eventing/Services/RabbitWorker.cs`, which already fanned
`GamePointRecord`/`GameBoard` into the games notifier; it now also fans `AttendanceRecord`
into the attendance one. A pickup request and a sign-out are both writes to the same record,
so that one entity type covers everything the wall renders. Three files outside the backend
station's list were touched for this — `GameDataChangeNotifier.cs`, one type name in
`GameDisplayService.cs`, and `RabbitWorker.cs` — none of them owned by another station.

### Smaller decisions

- **Migration**: `Data/Migrations/20260827132110_PickupRequests.cs` — two nullable columns, an
  index and a FK on `PickupRequestedUserId`. Additive only; no backfill was needed or written.
  Note the migrations folder is `Grpc/Data/Migrations`, not `Grpc/Migrations`.
- **`RequestPickup` only writes when the state actually changes.** Marking an unchanged
  property writes the stale read back, which is the same two-writer bug in a smaller window
  (`Grpc/AGENTS.md`). A press that agrees with what is already stored is a success that emits
  no `UPDATE` and no event.
- **Today's service is resolved on the *local* date**, matching `Tool.razor.cs RetrieveService`
  as instructed. `GameDisplayService.ResolveServiceAsync` uses `DateTime.UtcNow.Date` for the
  same job, which drifts for an evening service — a Friday night here is already Saturday in
  UTC. The two wall displays will therefore disagree about which service is "today" late on a
  service night. Not fixed here: `GameDisplayService` is another feature's file and changing
  which service the scoreboard resolves to is not a backend-station call. Worth a follow-up.
- **`Deleted` is filtered** on the wall query, and a soft-deleted record is still accepted by
  `RequestPickup` — it simply never reaches the wall. Flagged rather than fixed because the
  spec is silent on it and rejecting would need a new error constant.

## Who may ask the pickup wall — the display key

> **Superseding note, 2026-08-27.** An earlier draft of this section, and any station brief
> written from it, asked for a **time window** on the pickup response (serving names only
> around the service time). That is **out of scope and must not be built** — see "What this
> does and does not defend against" below for the owner's reasoning. Everything else in this
> section stands. If a brief you were given says to build the window, this note wins.


Names on an anonymous screen was decided above. *Who can open that screen* is a separate
question, and the plan as first written did not answer it: a live list of which children are
in a known building at a known hour, served to anyone who types the URL.

### The page URL is not the control point

`/Display/Pickup` is served by the YARP `{**catch-all}` route to the WASM cluster. **Every
route returns the same `index.html`** and the Blazor router picks the page client side, so
there is nothing at that URL to protect — the bundle is public and always was. The only
thing worth gating is the data call,
`public/GSBC.ImpactKids.Attendance.Display/WatchPickups`.

A key checked on the page route would look like security and be none.

### Enrol on a query string, run on a cookie

The key rides the query string **once**, and a cookie carries it after that:

```
TV bookmark ──► /bff/display-login?key=…  ──► sets cookie ──► 302 ──► /Display/Pickup
                                                                          │
                                          WatchPickups ◄── cookie ────────┘
```

`WatchPickups` is a long-lived stream that reconnects. A key left in the query string is a
credential written into proxy and CDN access logs on **every reconnect, forever**; a key
spent once at enrolment appears there once. `DevAuthEndpoints.MapDevAuthEndpoints` is the
existing precedent for this exact shape — mint a session, redirect to a clean URL.

The TV bookmarks the *keyed* URL, so a cookie lost to a browser restart or a wiped profile
re-enrols itself with no human involved. That preserves the property that made a
non-expiring key attractive in the first place: bookmark once, works every Sunday.

### Rules

- **The key lives in the database, not config.** "Rotated on admin request" means someone
  presses a button, not that someone redeploys. It also means rotation can show the new URL
  back to the admin who asked for it.
- **Non-expiring, rotated only on request.** A key that expires on its own strands a TV
  mid-service, which is worse than the risk it manages.
- **Rotation is immediate and total.** Every enrolled screen falls back to the unauthorised
  state at once and must be re-opened from the new link. That is the point of rotating.
- **The cookie is its own scheme, not the leader session.** It grants exactly one thing:
  calling the pickup display service. It must never satisfy `Policies.EnabledOnly`, and it
  must never be accepted on a `gRPC/` route.
- **Compare in constant time**, and never log the key — not on success, not on failure, not
  in a redirect the proxy will record.

### Lifetime: long lived at both hops, on purpose

The key does not expire, and **the cookie it mints does not expire either**. Both are long
lived by decision, not by omission.

The failure this avoids is specific: a screen that works every Sunday for a year and then,
one Sunday, silently shows nothing because a lifetime nobody remembers setting has run out.
Nobody is standing at that screen to notice. A credential that strands a wall mid service is
worse than the risk its expiry was managing.

So:

- The key changes **only** when an admin asks for it.
- The cookie's lifetime is not a second, shorter clock hiding behind the key. Give it a far
  future expiry and a sliding renewal, so a TV left on does not age out.
- The TV bookmarks the **keyed** URL. If the cookie is ever lost anyway — a wiped profile, a
  browser update, a factory reset — opening the bookmark re-enrols it with nobody involved.

The cookie exchange is therefore not about shortening anything. It is only about keeping the
key out of proxy and CDN access logs on every one of a long lived stream's reconnects. The
operational property is unchanged: set it up once, it works.

### What this does and does not defend against

It stops the URL being guessed, crawled, or idly shared. That is the whole of its job.

It does **not** help against anyone who has ever held the URL. There is no time window on the
data and no second control behind the key: **the key is the control.**

A time-boxed response — serving names only around the service time — was considered and
**declined by the owner**, on the grounds that the TV and the link are under his sole control
and nobody else sees either. That is a deliberate acceptance of the residual risk by the
person who holds it, and it is recorded here so that a later reader does not mistake its
absence for an oversight and quietly add one. If the link ever leaves that person's control,
rotating the key is the answer, and rotation is immediate and total.

### The unauthorised state is a screen, not a failure

A missing or stale cookie makes `WatchPickups` answer 401. The wall must render that as
readable words — that the screen needs re-opening from its setup link — and never as
"Connecting…", which is what a wall shows when nobody has noticed it is broken.
