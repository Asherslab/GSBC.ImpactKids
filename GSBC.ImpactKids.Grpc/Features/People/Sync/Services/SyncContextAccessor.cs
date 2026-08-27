using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public class SyncContextAccessor : ISyncContextAccessor
{
    private static readonly AsyncLocal<SyncSource> CurrentContext = new();

    public SyncSource Current => CurrentContext.Value;

    public IDisposable SetSource(SyncSource source)
    {
        SyncSource previous = CurrentContext.Value;
        CurrentContext.Value = source;
        return new Restore(previous);
    }

    private sealed class Restore(SyncSource previous) : IDisposable
    {
        public void Dispose() => CurrentContext.Value = previous;
    }
}
