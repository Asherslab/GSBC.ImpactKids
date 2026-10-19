using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// +5, −1. Relative, not "set to 20": two leaders each pressing +5 at the same moment mean +10, and
    /// a relative change applied under the lock gets exactly that. A change that would take the item
    /// below zero takes it to zero.
    /// </summary>
    public async Task<BasicResponse> AdjustItem(AdjustScheduleItemRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfItem(request.ItemId, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, edit) =>
        {
            DbScheduleItem? item = FindItem(schedule, request.ItemId);

            if (item == null)
                return BasicResponse.WithError(ScheduleItemNotFound);

            int current = item.PlannedMinutes + item.AdjustmentMinutes;
            int wanted  = Math.Clamp(current + request.Minutes, 0, ScheduleRules.MaxMinutes);

            ChangeLength(item, wanted - current, edit);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
