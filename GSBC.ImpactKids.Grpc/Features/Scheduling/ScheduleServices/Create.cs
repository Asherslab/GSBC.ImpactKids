using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>A new, empty template. Its first item creates the block it lives in.</summary>
    public async Task<BasicReadResponse<Guid?>> Create(CreateScheduleTemplateRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BasicReadResponse<Guid?>.WithError(ScheduleNameNull);

        Guid scheduleId = Guid.NewGuid();

        db.Schedules.Add(new DbSchedule
        {
            Id = scheduleId,
            Name = request.Name.Trim()
        });

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = scheduleId,
            Success = true
        };
    }
}
