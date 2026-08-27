using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Data.Models.Sync;

/// <summary>
/// One decision a sync made, recorded before anything acts on it.
///
/// The two observed hashes are not new machinery — they are the base value's primitive at a
/// different moment. Apply re-reads both sides and compares against them, and an item whose reading
/// has moved is marked <see cref="PlannedChangeStatus.Stale"/> and skipped rather than applied.
/// </summary>
public class DbSyncPlannedChange
{
    public required Guid             Id              { get; set; }
    public required Guid             SyncOperationId { get; set; }
    public          DbSyncOperation? SyncOperation   { get; set; }

    /// <summary>Null only for a create-from-Elvanto, where no app person exists yet.</summary>
    public          Guid?            PersonId        { get; set; }

    /// <summary>Null only for a create-in-Elvanto, where no Elvanto record exists yet.</summary>
    public          string?          ElvantoId       { get; set; }

    public required PlannedChangeKind Kind      { get; set; }

    /// <summary>Null for the four kinds that are about a person rather than a field.</summary>
    public          string?           FieldName { get; set; }

    public string? ObservedAppValue     { get; set; }
    public string? ObservedAppHash      { get; set; }
    public string? ObservedElvantoValue { get; set; }
    public string? ObservedElvantoHash  { get; set; }

    /// <summary>The value that would be written or pushed.</summary>
    public string? ProposedValue { get; set; }

    /// <summary>The reconciler's own words — <c>LastWriteWins:AppNewer</c>, <c>ElvantoChangedAlone</c>.</summary>
    public required string Reason { get; set; }

    public required PlannedChangeStatus Status       { get; set; }
    public          string?             StatusReason { get; set; }

    public required DateTimeOffset  DecidedAt { get; set; }
    public          DateTimeOffset? AppliedAt { get; set; }

    public DbPerson? Person { get; set; }
}
