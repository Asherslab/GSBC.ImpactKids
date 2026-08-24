namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record SyncManualReviewEntry
{
    public required Guid               Id              { get; init; }
    public required Guid               PersonId        { get; init; }
    public required string             ElvantoId       { get; init; }
    public          string?            PersonName      { get; init; }
    public          string?            MatchStrategy   { get; init; }
    public required int                MatchConfidence { get; init; }
    public required ManualReviewStatus Status          { get; init; }
    public          DateTime?          ReviewedAt      { get; init; }
    public required DateTime           CreatedAt       { get; init; }
}

[ProtoContract]
public enum ManualReviewStatus
{
    Pending  = 0,
    Approved = 1,
    Denied   = 2
}
