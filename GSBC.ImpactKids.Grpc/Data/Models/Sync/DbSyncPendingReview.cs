using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Data.Models.Sync;

public class DbSyncPendingReview
{
    public required Guid               Id              { get; set; }
    public required Guid               PersonId        { get; set; }
    public          DbPerson?          Person          { get; set; }
    public required string             ElvantoId       { get; set; }
    public          int                MatchConfidence { get; set; }
    public          string?            MatchStrategy   { get; set; }
    public required ManualReviewStatus Status          { get; set; }
    public required DateTimeOffset     CreatedAt       { get; set; }
    public          DateTimeOffset?    ReviewedAt      { get; set; }
    public          string?            PersonName      { get; set; }
}
