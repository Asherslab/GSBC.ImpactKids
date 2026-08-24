using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.People;

namespace GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;

public partial class SyncService
{
    public async Task<SyncResponse> CreateSync(SyncWithElvantoRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        var result = await syncEngine.SyncAsync(request, token);

        await eventService.SendUpdatedEvent(token);
        await personEventService.SendUpdatedEvent(token);

        return syncResultConverter.Convert(result);
    }
}
