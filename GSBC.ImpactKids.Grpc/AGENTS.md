# Services, data and migrations

One code-first gRPC service per feature, laid out as a partial class with one file per operation —
the idiom is below. Add a new operation as a new file, not as another method in an existing one.

Every service must be mapped in `Program.cs` — `app.MapGrpcService<XService>()`. A service that
compiles and is not mapped fails at the client as an unimplemented method.

# Partial classes — the house idiom

This is how nearly every type of any size in this project is laid out: 112 files declare a
`partial class`, and 23 of the 27 service classes use it. Learn the shape once and every feature
folder reads the same.

## Flavour one — a folder per service, a file per operation

```
Features/People/AllergyServices/
    AllergyService.cs      <- the root: attributes, primary constructor, nothing else
    Create.cs              <- public partial class AllergyService { public async Task<…> Create(…) }
    Update.cs
    ReadMultiple.cs
    Delete.cs
```

The root file carries the whole declaration and often no body at all:

```csharp
[Authorize(Policy = Policies.EnabledOnly)]
public partial class SyncService(
    GsbcDbContext                              db,
    IConverter<DbSyncOperation, SyncOperation> operationConverter,
    IElvantoPersonSyncService                  syncEngine
) : ISyncService;
```

Primary-constructor parameters are in scope in **every** partial file, and that is the whole reason
the idiom works. An operation file names `db` and `operationConverter` directly — no fields, no
`this.`, no constructor boilerplate to keep in sync across files. Adding a dependency is one line
in one file and it is immediately available to every operation.

Members that genuinely belong to the type as a whole — a shared `JsonSerializerOptions`, a gate
property every operation asks — live in the root file beside the constructor.
`ElvantoServices/ElvantoService.cs` is the reference for that.

## Flavour two — not only for gRPC services

The same layout applies to any injected service with more than one job.
`Features/Elvanto/ElvantoServices/` is a plain `AddScoped` service, not a mapped gRPC endpoint, and
splits identically: `ElvantoService.cs` (primary constructor, the write gates, the single
`SendMessage` choke point) beside `GetPeople.cs`, `CreatePerson.cs`, `UpdatePerson.cs`,
`GetPersonInfo.cs`, `GetElvantoReport.cs` and `GetServicePositions.cs`.

Do not reach for a different structure just because a service is internal.

## Flavour three — one type split by area, in place

When a type is not a service and has no "operations" — `GsbcDbContext` — split by subject area
using a dotted suffix in the same folder rather than a subfolder:

```
Data/GsbcDbContext.cs                 <- DbSets and OnModelCreating, calling into each area
Data/GsbcDbContext.PeopleModel.cs
Data/GsbcDbContext.SyncModel.cs
Data/GsbcDbContext.GamesModel.cs
```

Use this form for a partial that is *a section of one type*, and flavour one for a partial that is
*one operation of a service*.

## Size

The largest partial file in this project is 273 lines (`ElvantoServices/GetPeople.cs`) and the
median is well under a hundred. A file crossing roughly 250 lines is the signal to ask whether it
is holding two operations.

`LoginService` (39 lines), `MetabaseService` (43) and `GameDisplayService` (344) are single-file
and not partial, which is fine — with one job and no second operation, a folder of files is
ceremony. Split when the second operation arrives, not before.

## When partials are the wrong answer

Partials split a **file**. They cannot split a **method**, and they do nothing about state shared
between the pieces. A type whose length comes from one long method over many shared mutable locals
gets *worse* under partials: the same tangle, now spread across files where you can no longer see
it all at once.

The test: can the piece you want to move out be understood from its parameters and the primary
constructor alone? If yes, a partial file is right. If it only makes sense while holding six
variables from the middle of another method in your head, the fix is to name that state as a type
and pass it — a collaborator object, not another `.cs`.

`Features/People/Sync/Services/ElvantoPersonSyncService.cs` is the live counter-example: 1270
lines, not partial, with one 728-line method over fifteen shared mutable locals. It needs both
moves — the self-contained helpers into partial files, and the phases that share state named as
collaborators. See [docs/work/2026-08-elvanto-sync-refactor.md](../docs/work/2026-08-elvanto-sync-refactor.md).

# Authorization

- `[Authorize(Policy = Policies.EnabledOnly)]` on the service class. 22 of the 24 services mapped in
  `Program.cs` carry it; the two exceptions are deliberate:
  - `LoginService` — `[Authorize]` only, because it is what a user hits before they are enabled.
  - `GameDisplayService` — no attribute at all, routed under `public/` for the wall display, which
    cannot sign in. **Only aggregate scores may go through it** — no people, no medical detail.
    Adding a method here is a decision about what an unauthenticated screen can read.
  - `AttendancePickupDisplayService` — no attribute either, routed under `public/` for the pickup
    wall. It deliberately carries **people**, so it has its own narrower rule rather than sharing the
    games one: a display name (first name plus last initial) and a time, for children currently
    requested and not yet signed out, and nothing else. It is also the only `public/` route with an
    `AuthorizationPolicy` on it — the screen enrols with a key, which is the whole of the control
    here; there is deliberately no time window behind it. See
    [docs/modules/auth/sign-in.md](../docs/modules/auth/sign-in.md).

  `EventingChannelsService` is a singleton helper, not a mapped gRPC service; it needs no policy.
- `Policies.EnabledOnly` requires the claim `Enabled=true`, which `CustomClaimsTransformation` adds by
  looking the caller's `sub` up in `Users` — not something Auth0 sends. An unknown `sub` is inserted as
  a **disabled** user, so a new account gets 403 until someone enables it on the admin page. The whole
  path, including the local sign-in bypass, is in
  [docs/modules/auth/sign-in.md](../docs/modules/auth/sign-in.md).
- There is no per-permission authorization in this repo — no `IAuthorizationService`, no permission
  constants. Access is binary: an enabled user can do everything. Do not write code that implies
  otherwise, and if fine-grained permissions are wanted, that is a design conversation first.

# Errors

All error strings live in `GSBC.ImpactKids.Shared.Contracts/ErrorConstants.cs`, globally imported via
`GlobalUsings.cs` so services reference them bare (`return BasicReadResponse<Guid?>.WithError(PersonNotFound)`).

**Use an existing constant before adding one.** Never return an inline string — the client shows the
error text to a leader on a phone, and the constants file is the only place the wording is reviewable.

# `db.Update(entity)` writes every column

`DbContext.Update` marks the **whole entity** modified, so `SaveChanges` emits an `UPDATE` listing every
column with the values the entity was *read* with. Any column another writer committed since that read
is silently reverted. This repo uses whole-entity `Update` in about 15 places and mostly gets away with
it — it only bites when two paths touch the same row.

Two phones on the same night is exactly that situation. Write only the columns the code owns:

```csharp
record.Deleted = true;

db.Entry(record).Property(x => x.Deleted).IsModified = true;
await db.SaveChangesAsync(token);   // UPDATE ... SET deleted = @p WHERE id = @id
```

Rules of thumb:

- Anything holding an entity across an `await`, or acting on a list loaded earlier, is a second writer.
  Mark properties, do not `Update`.
- Mark a property only when the value actually changed. Marking an untouched property writes the stale
  read back — the same bug in a smaller window.
- Re-read before deciding: `await db.Entry(x).ReloadAsync(token)`.
- `db.Update` is fine for a row one path owns end to end, and for a fresh graph passed to `Add`.

The related trap is client-side: a record deleted while its create was still in flight was dropped
locally and landed on the server anyway, double-counting points. Queued mutations and server truth have
to be reconciled in one place — see [docs/modules/games/README.md](../docs/modules/games/README.md).

# Deletes are soft where the row is evidence

`DbAttendanceRecord` and `DbGamePointRecord` carry a `Deleted` flag rather than being removed, and every
read filters it. A sign-in that never happened and a sign-in that was taken back are different facts,
and the second one is worth keeping. Follow that pattern for anything a leader can undo; filter
`!x.Deleted` in every query, including counts.

# Migrations

Applied by `GSBC.ImpactKids.Workers.DbMigrations`, which calls `MigrateAsync()` on startup and which the
AppHost waits for before starting the gRPC service. So a missing migration surfaces as the whole app
failing to start, not as a runtime error.

- `dotnet ef` is fine to run directly — it is the one CLI exception, since Rider has no MCP equivalent:

  ```bash
  dotnet ef migrations add <Name> --project GSBC.ImpactKids.Grpc
  ```

- Name a migration for what it does — `PlacementScoring`, not a timestamp slug. The generated timestamp
  already orders it.
- Additive is free: new columns (nullable, or with a default), new tables, new indexes, widening a type.
- Destructive needs asking first: dropping a column or table, narrowing a type, any backfill that
  rewrites existing rows. Propose it; do not generate it.
- Never suppress `PendingModelChangesWarning`. Nothing in this repo does today, and it must stay that
  way — it is the only thing that tells you a model has drifted from its migrations. If a context's
  model no longer matches, that is a migration owed, not a warning to silence.

# Dates

Database models use `DateTimeOffset`, contracts use UTC `DateTime`, and `DateTimeConverter` in
`Conversion/Converters.cs` bridges them. Keep new columns `DateTimeOffset` — it maps to `timestamptz`
regardless of Npgsql's legacy-timestamp switch, which this repo deliberately never sets.

**A `DateTimeOffset` you compare against in a query must have offset zero.** Npgsql refuses to write
any other offset to a `timestamptz`:

```
Cannot write DateTimeOffset with Offset=10:00:00 to PostgreSQL type
'timestamp with time zone', only offset 0 (UTC) is supported.
```

It **builds fine and throws at execution**, so it surfaces as a failed request rather than a
compiler error — and on a wall display with nobody standing at it, as "Connecting…" forever.

This bites exactly where the *logic* is correctly local. Working out "today" means the local day,
because a Friday evening service here is already Saturday in UTC — but the bounds must be converted
before they reach the query:

```csharp
DateTime       localToday = DateTime.Today;
DateTimeOffset dayStart   = new DateTimeOffset(localToday, TimeZoneInfo.Local.GetUtcOffset(localToday))
    .ToUniversalTime();          // same instant, offset 0 - without this it throws
```

Everywhere else in this project reaches offset zero via
`new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))`. Use that when the value is
already UTC, and `ToUniversalTime()` when you deliberately started from a local wall-clock day.

# Converters

Mapperly, registered through `AddConverters()`. A `[MapperIgnore]` on a navigation property is load
bearing: without it the mapper walks the graph and either serialises half the database or fails on a
cycle. New navigation properties on a `Db*` model need it.

# Events

Mutating operations end with `await eventService.SendUpdatedEvent(token)`, which pushes an invalidation
through RabbitMQ so other clients refresh. A create or update that skips it leaves every other phone
showing stale data until a manual refresh — easy to miss, because the phone that made the change looks
right.
