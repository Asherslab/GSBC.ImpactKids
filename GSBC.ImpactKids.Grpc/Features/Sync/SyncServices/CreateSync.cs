using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.People;

namespace GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;

public partial class SyncService
{
    /// <summary>
    /// <paramref name="request"/> carries nothing and is deliberately not passed on: an RPC needs a
    /// request type, and this one is a marker. Mode and Scope used to live on it.
    /// </summary>
    public async Task<SyncResponse> CreateSync(SyncWithElvantoRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        SyncResult result = await syncEngine.SyncAsync(token);

        await eventService.SendUpdatedEvent(token);
        await personEventService.SendUpdatedEvent(token);

        return syncResultConverter.Convert(result);
    }
}
