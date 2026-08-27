using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

public sealed record ConflictResolution(
    SyncSource WinningSide,
    string?    WinningValue,
    string     Reason
);
