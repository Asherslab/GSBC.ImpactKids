using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    public async Task<BasicResponse> RemoveItem(BasicReadRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfItem(request.Guid, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            DbScheduleLane? lane = schedule.Blocks.SelectMany(b => b.Lanes)
                .FirstOrDefault(l => l.Items.Any(i => i.Id == request.Guid));

            if (lane == null)
                return BasicResponse.WithError(ScheduleItemNotFound);

            DbScheduleItem item = lane.Items.Single(i => i.Id == request.Guid);

            lane.Items.Remove(item);
            db.ScheduleItems.Remove(item);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
