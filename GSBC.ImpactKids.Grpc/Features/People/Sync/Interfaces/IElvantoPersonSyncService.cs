using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

public interface IElvantoPersonSyncService
{
    Task<SyncResult> SyncAsync(SyncWithElvantoRequest request, CancellationToken token = default);
}
