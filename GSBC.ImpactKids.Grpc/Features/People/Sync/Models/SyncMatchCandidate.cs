using GSBC.ImpactKids.Grpc.Data.Models.People;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

public sealed record SyncMatchCandidate(
    DbPerson Person,
    int      Confidence,
    string   Strategy
);
