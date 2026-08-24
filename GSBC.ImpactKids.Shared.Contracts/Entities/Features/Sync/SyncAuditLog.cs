namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record SyncAuditLog
{
    public required Guid          Id              { get; init; }
    public required Guid          SyncOperationId { get; init; }
    public required Guid          PersonId        { get; init; }
    public required SyncEventType EventType       { get; init; }
    public          string?       FieldName       { get; init; }
    public          string?       FromValue       { get; init; }
    public          string?       ToValue         { get; init; }
    public          SyncSource?   Direction       { get; init; }
    public required string        Reason          { get; init; }
    public required DateTime      OccurredAt      { get; init; }

    [ProtoIgnore]
    public DateTime LocalOccurredAt => OccurredAt.ToLocalTime();
}

[ProtoContract]
public enum SyncEventType
{
    Match                = 0,
    FieldUpdated         = 1,
    Conflict             = 2,
    Created              = 3,
    PushedToElvanto      = 4,
    WouldPushToElvanto   = 5,
    WouldCreateInElvanto = 6,
    ManualReviewQueued   = 7,
    Archived             = 8
}

[ProtoContract]
public enum SyncSource
{
    App     = 0,
    Elvanto = 1
}