using GSBC.ImpactKids.Grpc.Data.Models.Sync;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

public sealed class SyncResult
{
    public required Guid            OperationId        { get; init; }
    public required bool            Success            { get; init; }
    public          string?         Error              { get; init; }
    public required int             PeopleProcessed    { get; init; }
    public required int             InboundPeople      { get; init; }
    public required int             InboundFields      { get; init; }
    public required int             OutboundPeople     { get; init; }
    public required int             OutboundFields     { get; init; }
    public required int             Conflicts          { get; init; }
    public required int             AutoLinked         { get; init; }
    public required int             ManualReviewQueued { get; init; }
    public required int             Archived           { get; init; }

    /// <summary>
    /// Fields whose two sides differ and which the engine chose not to act on. A non-zero count is
    /// not a failure; a count that never moves while people report missing changes is.
    /// </summary>
    public required int Diverged { get; init; }

    /// <summary>Items in the plan this run decided, or executed.</summary>
    public int PlannedChanges { get; init; }

    /// <summary>Items Apply refused because a side had moved since the plan was decided.</summary>
    public int StaleItems { get; init; }

    public List<ManualReviewItem> ManualReviewItems { get; init; } = [];
    public List<DbSyncAuditLog>   AuditLog          { get; init; } = [];

    /// <summary>
    /// One failure shape for every abort. There were four hand-written result literals, which is why
    /// the audit log was attached to the success one and absent from all three failures.
    /// </summary>
    public static SyncResult Failed(Guid operationId, string error) => new()
    {
        OperationId        = operationId,
        Success            = false,
        Error              = error,
        PeopleProcessed    = 0,
        InboundPeople      = 0,
        InboundFields      = 0,
        OutboundPeople     = 0,
        OutboundFields     = 0,
        Conflicts          = 0,
        AutoLinked         = 0,
        ManualReviewQueued = 0,
        Archived           = 0,
        Diverged           = 0
    };
}

public sealed record ManualReviewItem(
    Guid   PersonId,
    string ElvantoId,
    string Reason,
    int    MatchConfidence
);
