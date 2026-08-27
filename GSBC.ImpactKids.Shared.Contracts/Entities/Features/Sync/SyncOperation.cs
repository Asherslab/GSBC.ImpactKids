namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record SyncOperation : IIdentifiable
{
    public required Guid Id { get; init; }

    public required DateTime StartedAt { get; init; }

    [ProtoIgnore]
    public DateTime LocalStartedAt => StartedAt.ToLocalTime();

    public DateTime? CompletedAt { get; init; }

    [ProtoIgnore]
    public DateTime? LocalCompletedAt => CompletedAt?.ToLocalTime();

    public SyncStatus? Status        { get; init; }
    public string?     FailureReason { get; init; }

    /// <summary>After this, Apply refuses the whole plan rather than any part of it.</summary>
    public DateTime? PlanExpiresAt { get; init; }

    [ProtoIgnore]
    public DateTime? LocalPlanExpiresAt => PlanExpiresAt?.ToLocalTime();

    /// <summary>Items still awaiting execution. Set by the read, not stored on the row.</summary>
    public int PendingPlanItems { get; init; }

    [ProtoIgnore]
    public bool PlanIsExecutable =>
        PendingPlanItems > 0 && (PlanExpiresAt is null || PlanExpiresAt > DateTime.UtcNow);
}

[ProtoContract]
public enum SyncStatus
{
    Success      = 0,
    Failed       = 1,
    Conflict     = 2,
    ManualReview = 3
}