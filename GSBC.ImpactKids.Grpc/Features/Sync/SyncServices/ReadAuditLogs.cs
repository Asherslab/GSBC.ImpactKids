using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;

public partial class SyncService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<SyncAuditLog>> ReadAuditLogs(
        BasicReadRequest request,
        CallContext      context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        if (!Guid.TryParse(request.Id, out Guid operationId))
        {
            yield return new BasicReadMultipleResponse<SyncAuditLog> { Success = false, Error = "Invalid operation ID" };
            yield break;
        }

        IQueryable<DbSyncAuditLog> query = db.SyncAuditLogs
            .Where(x => x.SyncOperationId == operationId)
            .OrderBy(x => x.OccurredAt);

        await foreach (BasicReadMultipleResponse<SyncAuditLog> response in query.ReturnInBatches(auditLogConverter, token: token))
        {
            yield return response;
        }
    }
}
