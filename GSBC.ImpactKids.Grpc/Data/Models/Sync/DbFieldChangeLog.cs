using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Data.Models.Sync;

public class DbFieldChangeLog
{
    public required Guid            Id         { get; set; }
    public required string          EntityType { get; set; }
    public required Guid            EntityId   { get; set; }
    public required string          FieldName  { get; set; }
    public required string          ValueHash  { get; set; }
    public required DateTimeOffset  ChangedAt  { get; set; }
    public required SyncSource      Source     { get; set; }
}
