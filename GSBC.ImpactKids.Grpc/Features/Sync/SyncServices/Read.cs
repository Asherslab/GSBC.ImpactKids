using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

using DbPlannedChangeStatus = GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums.PlannedChangeStatus;

namespace GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;

public partial class SyncService
{
    public async Task<BasicReadResponse<SyncOperation>> Read(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (!Guid.TryParse(request.Id, out Guid id))
            return BasicReadResponse<SyncOperation>.WithError("Invalid sync operation ID");

        DbSyncOperation? operation = await db.SyncOperations
            .FirstOrDefaultAsync(x => x.Id == id, token);

        if (operation == null)
            return BasicReadResponse<SyncOperation>.WithError("Sync operation not found");

        int pending = await db.PlannedChanges
            .CountAsync(x => x.SyncOperationId == id && x.Status == DbPlannedChangeStatus.Pending, token);

        return new BasicReadResponse<SyncOperation>
        {
            Entity  = operationConverter.Convert(operation) with { PendingPlanItems = pending },
            Success = true
        };
    }
}
