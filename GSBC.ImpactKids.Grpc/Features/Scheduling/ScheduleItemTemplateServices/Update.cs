using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleItemTemplateServices;

public partial class ScheduleItemTemplateService
{
    public async Task<BasicResponse> Update(UpdateScheduleItemTemplateRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbScheduleItemTemplate? template = await db.ScheduleItemTemplates
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (template == null)
            return BasicResponse.WithError(ScheduleItemTemplateNotFound);

        if (request.Name.IsUpdated)
        {
            if (string.IsNullOrWhiteSpace(request.Name.Value))
                return BasicResponse.WithError(ScheduleItemNameNull);
            template.Name = request.Name.Value.Trim();
        }

        if (request.Minutes.IsUpdated)
        {
            if (!ScheduleRules.MinutesValid(request.Minutes.Value))
                return BasicResponse.WithError(ScheduleItemMinutesInvalid);
            template.Minutes = request.Minutes.Value;
        }

        if (request.StartsAt.IsUpdated)
        {
            if (!ScheduleRules.StartsAtValid(request.StartsAt.Value))
                return BasicResponse.WithError(ScheduleItemStartsAtInvalid);
            template.StartsAt = request.StartsAt.Value;
        }

        if (request.NeedsFillIn.IsUpdated)
            template.NeedsFillIn = request.NeedsFillIn.Value;

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}
