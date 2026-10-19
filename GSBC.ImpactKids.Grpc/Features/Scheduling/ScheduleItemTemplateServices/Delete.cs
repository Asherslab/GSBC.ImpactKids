using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleItemTemplateServices;

public partial class ScheduleItemTemplateService
{
    /// <summary>
    /// Schedules that used the template keep their copies of it; they only lose the link back
    /// (<c>SET NULL</c>), which is what comparing like with like across services reads.
    /// </summary>
    public async Task<BasicResponse> BasicDelete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        int deleted = await db.ScheduleItemTemplates
            .Where(x => x.Id == request.Guid)
            .ExecuteDeleteAsync(token);

        if (deleted == 0)
            return BasicResponse.WithError(ScheduleItemTemplateNotFound);

        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}
