using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

public sealed class SyncResult
{
    public required Guid      OperationId        { get; init; }
    public required ElvantoSyncMode Mode               { get; init; }
    public required bool      Success            { get; init; }
    public          string?   Error              { get; init; }
    public required int       PeopleProcessed    { get; init; }
    public required int       InboundPeople      { get; init; }
    public required int       InboundFields      { get; init; }
    public required int       OutboundPeople     { get; init; }
    public required int       OutboundFields     { get; init; }
    public required int       Conflicts          { get; init; }
    public required int       AutoLinked         { get; init; }
    public required int       ManualReviewQueued { get; init; }
    public required int       Archived           { get; init; }

    /// <summary>
    /// Fields whose two sides differ and which the engine chose not to act on. A non-zero count is
    /// not a failure; a count that never moves while people report missing changes is.
    /// </summary>
    public required int       Diverged           { get; init; }

    public List<ManualReviewItem>  ManualReviewItems { get; init; } = [];
    public List<DbSyncAuditLog>    AuditLog          { get; init; } = [];
}

public sealed record ManualReviewItem(
    Guid   PersonId,
    string ElvantoId,
    string Reason,
    int    MatchConfidence
);
