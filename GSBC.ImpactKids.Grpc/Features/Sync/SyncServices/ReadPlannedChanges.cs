using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;

public partial class SyncService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<SyncPlannedChange>> ReadPlannedChanges(
        BasicReadRequest request,
        CallContext      context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        if (!Guid.TryParse(request.Id, out Guid operationId))
        {
            yield return new BasicReadMultipleResponse<SyncPlannedChange> { Success = false, Error = "Invalid operation ID" };
            yield break;
        }

        IQueryable<DbSyncPlannedChange> query = db.PlannedChanges
            .Where(x => x.SyncOperationId == operationId)
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.FieldName)
            .ThenBy(x => x.PersonId);

        await foreach (BasicReadMultipleResponse<SyncPlannedChange> response in
                       query.ReturnInBatches(plannedChangeConverter, token: token))
        {
            yield return response;
        }
    }
}
