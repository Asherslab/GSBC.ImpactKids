# Services, data and migrations

One code-first gRPC service per feature, each a `partial class` with one file per operation
(`Create.cs`, `Update.cs`, `ReadMultiple.cs`, `Delete.cs`) beside a root file holding the primary
constructor. Add a new operation as a new file, not as another method in an existing one.

Every service must be mapped in `Program.cs` — `app.MapGrpcService<XService>()`. A service that
compiles and is not mapped fails at the client as an unimplemented method.

# Authorization

- `[Authorize(Policy = Policies.EnabledOnly)]` on the service class. 22 of the 24 services mapped in
  `Program.cs` carry it; the two exceptions are deliberate:
  - `LoginService` — `[Authorize]` only, because it is what a user hits before they are enabled.
  - `GameDisplayService` — no attribute at all, routed under `public/` for the wall display, which
    cannot sign in. **Only aggregate scores may go through it** — no people, no medical detail.
    Adding a method here is a decision about what an unauthenticated screen can read.

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

# Converters

Mapperly, registered through `AddConverters()`. A `[MapperIgnore]` on a navigation property is load
bearing: without it the mapper walks the graph and either serialises half the database or fails on a
cycle. New navigation properties on a `Db*` model need it.

# Events

Mutating operations end with `await eventService.SendUpdatedEvent(token)`, which pushes an invalidation
through RabbitMQ so other clients refresh. A create or update that skips it leaves every other phone
showing stale data until a manual refresh — easy to miss, because the phone that made the change looks
right.
