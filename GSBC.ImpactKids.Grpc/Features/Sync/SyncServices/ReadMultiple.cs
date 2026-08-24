using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;

public partial class SyncService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<SyncOperation>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbSyncOperation> query = db.SyncOperations
            .OrderByDescending(x => x.StartedAt);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<SyncOperation> response in query.ReturnInBatches(operationConverter, token: token))
        {
            yield return response;
        }
    }
}
