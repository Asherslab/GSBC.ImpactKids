# Contracts

Shared by the server and the WASM client, serialised with protobuf-net. Every consumer is in this repo,
so contracts change freely — but both ends must be rebuilt together.

- All serialised types carry `[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]`. Always
  include `ImplicitFields` — without it the type serialises as nothing and the failure is silent.
- Use `record` where possible. Some types are still `class` (`BasicResponse` and its descendants);
  check the base before choosing.
- Service interfaces implement the base interfaces in `Services/Base` for the methods they use —
  `IBasicReadMultipleService`, `ICreateService`, `IUpdateService`, `IBasicDeleteService`,
  `IBasicMultipleRelationshipService`.
- Method names are not prefixed or suffixed with the entity name: `Create`, `Update`, `BasicDelete`,
  `BasicReadMultiple`.

## Messages

- Before writing a new message, read the base classes in `Messages/Requests/Base` and
  `Messages/Responses/Base`. **Do not create a custom message with no additional properties** — use the
  base directly.
- If a custom message is needed, extend a base class unless none applies.
- All responses implement `ISuccessResponse` and `IErrorResponse` (via `BasicResponse`,
  `BasicReadResponse<T>` or `BasicReadMultipleResponse<T>`).
- A response carrying a single value — a `Guid`, a count — uses `BasicReadResponse<T>` even when the
  request was not a read. `Create` returning the new id is the common case. Do not invent an entity for
  a one-off payload.

## Structure

```
Entities/Features/<Feature>/          grouped by domain feature: People, Games, Attendance
Messages/Requests/Features/<Feature>/
Messages/Responses/Features/<Feature>/
Services/Features/<Feature>/
```

Entities group by domain area (`People`, `Games`, `Scripture`). Messages group by the entity they act on
(`People`, `AttendanceRecords`, `GamePointRecords`). Service interfaces are one per feature
(`IPersonService`, `ISchoolGradeService`).

## `PaginationRequest` silently truncates to 10

`PerPage` defaults to 10, and `QueryableExtensions.Paginate` applies that default when `Pagination` is
null (`GSBC.ImpactKids.Grpc/Extensions/QueryableExtensions.cs:10`). So a `BasicReadMultipleRequest`
built without pagination returns **the first ten rows and no indication there are more**.

Use `BasicReadMultipleRequest.All()` — or `PaginationRequest.All()`, which sets `Disabled` — whenever
the caller wants everything. A store backing a page that lists people or games wants everything; if a
list looks mysteriously short, this is the first thing to check.

## Dates cross the wire as `DateTime`, in UTC

Contracts use `DateTime`, never `DateTimeOffset` — protobuf-net has no surrogate for it here, and there
are currently zero `DateTimeOffset` properties in this project. The database models use
`DateTimeOffset` (`timestamptz`), so the conversion happens in the converters, and the entity exposes a
`Local*` projection for the UI:

```csharp
public required DateTime? DateOfBirth { get; init; }

[ProtoIgnore]
public DateTime? LocalDateOfBirth => DateOfBirth?.ToLocalTime();
```

Bind UI inputs to the `Local*` property and let the request carry UTC. `[ProtoIgnore]` on the projection
matters: `ImplicitFields.AllPublic` would otherwise serialise a computed property and assign it a field
number.

## Shared rules belong here, not on one side

A rule both ends must agree on goes in this project, as a constant or a helper — never as matching
copies on the phone and the server. When the two sides disagree about a rule, every later step slides:
`CountsTowardNight` was the case that proved it (see
[docs/modules/games/README.md](../docs/modules/games/README.md)), and the school-grade tiers in
`Entities/Features/People/SchoolGradeTiers.cs` are the same shape — one list, read by the attendance
tool and the memorisation table alike.
