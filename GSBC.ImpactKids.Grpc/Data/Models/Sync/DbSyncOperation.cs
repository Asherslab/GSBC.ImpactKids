using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Data.Models.Sync;

public class DbSyncOperation
{
    public required Guid             Id          { get; set; }
    public required SyncMode         Mode        { get; set; }
    public required SyncScope        Scope       { get; set; }
    public          Guid?            PersonId    { get; set; }
    public          DbPerson?        Person      { get; set; }
    public          Guid?            FamilyId    { get; set; }
    public required DateTimeOffset   StartedAt   { get; set; }
    public          DateTimeOffset?  CompletedAt   { get; set; }
    public          SyncStatus?      Status        { get; set; }
    public          string?          FailureReason { get; set; }

    /// <summary>
    /// After this, Apply refuses the whole plan rather than any part of it.
    ///
    /// The per-item staleness check is the real protection; this is a backstop against a failure it
    /// cannot catch. A stale item is one whose <i>values</i> moved. Expiry guards against the
    /// <i>set</i> of items being wrong — people created, deleted or merged in Elvanto since Decide
    /// ran, which no per-item check can see because those items are not in the plan.
    /// </summary>
    public          DateTimeOffset?  PlanExpiresAt { get; set; }

    public List<DbSyncAuditLog>      AuditLogs      { get; set; } = [];
    public List<DbSyncPlannedChange> PlannedChanges { get; set; } = [];
}
