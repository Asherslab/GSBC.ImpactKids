using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>A drag: to any lane of the same schedule, at a position in that lane's list.</summary>
    public async Task<BasicResponse> MoveItem(MoveScheduleItemRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfItem(request.ItemId, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            List<DbScheduleLane> lanes = schedule.Blocks.SelectMany(b => b.Lanes).ToList();

            DbScheduleLane? from = lanes.FirstOrDefault(l => l.Items.Any(i => i.Id == request.ItemId));
            DbScheduleLane? to   = lanes.FirstOrDefault(l => l.Id == request.LaneId);

            if (from == null)
                return BasicResponse.WithError(ScheduleItemNotFound);

            if (to == null)
                return BasicResponse.WithError(ScheduleNotFound);

            DbScheduleItem item = from.Items.Single(i => i.Id == request.ItemId);

            from.Items.Remove(item);
            item.LaneId = to.Id;
            to.Items.Insert(Math.Clamp(request.Position, 0, to.Items.Count), item);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
