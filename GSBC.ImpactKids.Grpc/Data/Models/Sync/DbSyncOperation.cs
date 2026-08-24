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

    public List<DbSyncAuditLog> AuditLogs { get; set; } = [];
}
