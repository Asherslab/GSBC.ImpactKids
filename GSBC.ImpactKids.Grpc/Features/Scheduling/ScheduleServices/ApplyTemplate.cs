using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>Replaces the service's schedule with a fresh copy of the template. The phone confirms first.</summary>
    public async Task<BasicReadResponse<Guid?>> ApplyTemplate(
        ApplyScheduleTemplateRequest request,
        CallContext                  context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        if (!await db.Services.AnyAsync(x => x.Id == request.ServiceId, token))
            return BasicReadResponse<Guid?>.WithError(ServiceNotFound);

        // The delete of the old schedule and the insert of the copy land together or not at all.
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(token);

        Guid? scheduleId = await ScheduleCopy.StageForService(db, request.TemplateId, request.ServiceId, token);

        if (scheduleId == null)
            return BasicReadResponse<Guid?>.WithError(ScheduleNotFound);

        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = scheduleId,
            Success = true
        };
    }
}
