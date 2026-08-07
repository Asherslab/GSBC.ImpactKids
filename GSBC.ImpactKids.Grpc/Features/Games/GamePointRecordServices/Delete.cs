using GSBC.ImpactKids.Grpc.Data.Models.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Games.GamePointRecordServices;

public partial class GamePointRecordService
{
    public async Task<BasicResponse> BasicDelete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbGamePointRecord? record = await db.GamePointRecords
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (record == null)
            return BasicResponse.WithError(GamePointRecordNotFound);

        record.Deleted = true;

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse { Success = true };
    }
}
