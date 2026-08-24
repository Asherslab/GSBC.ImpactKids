using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

public interface ISyncContextAccessor
{
    SyncSource Current { get; }
    IDisposable SetSource(SyncSource source);
}
