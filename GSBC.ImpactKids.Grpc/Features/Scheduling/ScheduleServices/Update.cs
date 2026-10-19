using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>A template's name, and which service type - if any - it is the default for.</summary>
    public async Task<BasicResponse> Update(UpdateScheduleTemplateRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbSchedule? template = await db.Schedules.FirstOrDefaultAsync(x => x.Id == request.Id, token);

        if (template == null)
            return BasicResponse.WithError(ScheduleNotFound);

        if (template.ServiceId != null)
            return BasicResponse.WithError(ScheduleNotATemplate);

        if (string.IsNullOrWhiteSpace(request.Name))
            return BasicResponse.WithError(ScheduleNameNull);

        if (request.DefaultForServiceTypeId != null)
        {
            if (!await db.ServiceTypes.AnyAsync(x => x.Id == request.DefaultForServiceTypeId, token))
                return BasicResponse.WithError(ServiceTypeNotFound);

            // Checked here for a readable message; the unique index is what actually holds it.
            if (await db.Schedules.AnyAsync(x => x.Id != template.Id
                                                 && x.DefaultForServiceTypeId == request.DefaultForServiceTypeId, token))
                return BasicResponse.WithError(ScheduleDefaultTaken);
        }

        template.Name = request.Name.Trim();
        template.DefaultForServiceTypeId = request.DefaultForServiceTypeId;

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse { Success = true };
    }
}
