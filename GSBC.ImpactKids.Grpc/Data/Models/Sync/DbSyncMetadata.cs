using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Data.Models.Sync;

public class DbSyncMetadata
{
    public required Guid        Id                  { get; set; }
    public required Guid        PersonId            { get; set; }
    public          DbPerson?   Person              { get; set; }
    public required string      ElvantoId           { get; set; }
    public          DateTimeOffset? LastSyncAt      { get; set; }
    public          SyncStatus? LastSyncStatus      { get; set; }
    public          int         MatchConfidence      { get; set; }
    public          DateTimeOffset? MatchedAt       { get; set; }
    public          string?     MatchStrategy       { get; set; }
    public          string?     ManualReviewReason  { get; set; }
}
