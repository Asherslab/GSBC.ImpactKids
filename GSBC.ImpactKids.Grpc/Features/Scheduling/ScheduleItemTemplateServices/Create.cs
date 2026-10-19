using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleItemTemplateServices;

public partial class ScheduleItemTemplateService
{
    public async Task<BasicReadResponse<Guid?>> Create(
        CreateScheduleItemTemplateRequest request,
        CallContext                       context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BasicReadResponse<Guid?>.WithError(ScheduleItemNameNull);

        if (!ScheduleRules.MinutesValid(request.Minutes))
            return BasicReadResponse<Guid?>.WithError(ScheduleItemMinutesInvalid);

        if (!ScheduleRules.StartsAtValid(request.StartsAt))
            return BasicReadResponse<Guid?>.WithError(ScheduleItemStartsAtInvalid);

        DbScheduleItemTemplate template = new()
        {
            Id = Guid.Empty,
            Name = request.Name.Trim(),
            Minutes = request.Minutes,
            StartsAt = request.StartsAt,
            NeedsFillIn = request.NeedsFillIn
        };

        await db.ScheduleItemTemplates.AddAsync(template, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = template.Id,
            Success = true
        };
    }
}
