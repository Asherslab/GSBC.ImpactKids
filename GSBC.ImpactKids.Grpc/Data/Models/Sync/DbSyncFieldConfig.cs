using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Data.Models.Sync;

public class DbSyncFieldConfig
{
    public required Guid           Id              { get; set; }
    public required string         EntityType      { get; set; }
    public required string         FieldName       { get; set; }
    public required SyncDirection  Direction       { get; set; }
    public required PrecedenceOnTie PrecedenceOnTie { get; set; }
}
