using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Data.Models.Sync;

public class DbSyncAuditLog
{
    public required Guid             Id              { get; set; }
    public required Guid             SyncOperationId { get; set; }
    public          DbSyncOperation? SyncOperation   { get; set; }
    public required Guid             PersonId        { get; set; }
    public required SyncEventType    EventType       { get; set; }
    public          string?          FieldName       { get; set; }
    public          string?          FromValue       { get; set; }
    public          string?          ToValue         { get; set; }
    public          SyncSource?      Direction       { get; set; }
    public required string           Reason          { get; set; }
    public required DateTimeOffset   OccurredAt      { get; set; }
}