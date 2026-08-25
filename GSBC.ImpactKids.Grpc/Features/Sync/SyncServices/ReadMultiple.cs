using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

using DbPlannedChangeStatus = GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums.PlannedChangeStatus;

namespace GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;

public partial class SyncService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<SyncOperation>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        // One count query for the whole list rather than one per row: the Execute action needs to
        // know which operations still have work waiting, and the list page shows every operation.
        Dictionary<Guid, int> pendingByOperation = await db.PlannedChanges
            .Where(x => x.Status == DbPlannedChangeStatus.Pending)
            .GroupBy(x => x.SyncOperationId)
            .Select(g => new { OperationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OperationId, x => x.Count, token);

        IQueryable<DbSyncOperation> query = db.SyncOperations
            .OrderByDescending(x => x.StartedAt);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<SyncOperation> response in query.ReturnInBatches(operationConverter, token: token))
        {
            yield return response with
            {
                Entities = response.Entities
                    .Select(x => x with { PendingPlanItems = pendingByOperation.GetValueOrDefault(x.Id) })
                    .ToImmutableList()
            };
        }
    }
}
