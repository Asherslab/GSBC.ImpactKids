using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// A template, or a service's schedule (the service is left with none). Services that were given
    /// a deleted template keep their copies.
    /// </summary>
    public async Task<BasicResponse> BasicDelete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        int deleted = await db.Schedules
            .Where(x => x.Id == request.Guid)
            .ExecuteDeleteAsync(token);

        if (deleted == 0)
            return BasicResponse.WithError(ScheduleNotFound);

        await eventService.SendUpdatedEvent(token);

        return new BasicResponse { Success = true };
    }
}
