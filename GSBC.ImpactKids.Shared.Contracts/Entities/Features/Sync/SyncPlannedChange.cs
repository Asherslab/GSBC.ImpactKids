namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;

/// <summary>
/// One decision a sync made, before anything acted on it. The plan is what a person reads before
/// pressing Execute, so every row has to explain itself: what it would do, to whom, and why.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record SyncPlannedChange : IIdentifiable
{
    public required Guid                Id              { get; init; }
    public required Guid                SyncOperationId { get; init; }
    public          Guid?               PersonId        { get; init; }
    public          string?             ElvantoId       { get; init; }
    public required PlannedChangeKind   Kind            { get; init; }
    public          string?             FieldName       { get; init; }
    public          string?             ObservedAppValue     { get; init; }
    public          string?             ObservedElvantoValue { get; init; }
    public          string?             ProposedValue        { get; init; }
    public required string              Reason          { get; init; }
    public required PlannedChangeStatus Status          { get; init; }
    public          string?             StatusReason    { get; init; }
    public required DateTime            DecidedAt       { get; init; }
    public          DateTime?           AppliedAt       { get; init; }

    [ProtoIgnore]
    public DateTime LocalDecidedAt => DecidedAt.ToLocalTime();
}

[ProtoContract]
public enum PlannedChangeKind
{
    InboundField    = 0,
    OutboundField   = 1,
    CreateInElvanto = 2,
    CreateLocally   = 3,
    Archive         = 4,
    LinkPerson      = 5
}

[ProtoContract]
public enum PlannedChangeStatus
{
    Pending = 0,
    Applied = 1,
    Skipped = 2,
    Stale   = 3,
    Failed  = 4
}
