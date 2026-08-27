using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.People;

namespace GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;

public partial class SyncService
{
    /// <summary>
    /// Executes a plan a person has read. Everything that makes this safe lives in the engine: the
    /// plan expires, every item's two sides are re-read before it is applied, and Apply executes
    /// only what the plan contains.
    /// </summary>
    public async Task<SyncResponse> ExecutePlan(ExecutePlanRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        SyncResult result = await syncEngine.ApplyPlanAsync(request.OperationId, token);

        await eventService.SendUpdatedEvent(token);
        await personEventService.SendUpdatedEvent(token);

        return syncResultConverter.Convert(result);
    }
}
