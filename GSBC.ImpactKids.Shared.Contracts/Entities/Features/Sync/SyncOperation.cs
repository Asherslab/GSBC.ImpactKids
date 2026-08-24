namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record SyncOperation : IIdentifiable
{
    public required Guid      Id       { get; init; }
    public required SyncMode  Mode     { get; init; }
    public required SyncScope Scope    { get; init; }
    public          Guid?     PersonId { get; init; }
    public          Guid?     FamilyId { get; init; }

    public required DateTime StartedAt { get; init; }

    [ProtoIgnore]
    public DateTime LocalStartedAt => StartedAt.ToLocalTime();

    public DateTime? CompletedAt { get; init; }

    [ProtoIgnore]
    public DateTime? LocalCompletedAt => CompletedAt?.ToLocalTime();

    public SyncStatus? Status        { get; init; }
    public string?     FailureReason { get; init; }
}

[ProtoContract]
public enum SyncMode
{
    Full    = 0,
    AppOnly = 1,
    DryRun  = 2
}

[ProtoContract]
public enum SyncScope
{
    All    = 0,
    Person = 1,
    Family = 2
}

[ProtoContract]
public enum SyncStatus
{
    Success      = 0,
    Failed       = 1,
    Conflict     = 2,
    ManualReview = 3
}