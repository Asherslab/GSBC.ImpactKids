using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// A drag: into a split's lane at a position among its items, or into the main list at a position
    /// among its entries - where a split counts as one.
    /// </summary>
    public async Task<BasicResponse> MoveItem(MoveScheduleItemRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfItem(request.ItemId, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            List<DbScheduleLane> lanes = schedule.Blocks.SelectMany(b => b.Lanes).ToList();

            DbScheduleLane? from = lanes.FirstOrDefault(l => l.Items.Any(i => i.Id == request.ItemId));

            if (from == null)
                return BasicResponse.WithError(ScheduleItemNotFound);

            DbScheduleItem item = from.Items.Single(i => i.Id == request.ItemId);

            if (request.LaneId == null)
            {
                from.Items.Remove(item);

                List<Entry> entries = Entries(schedule);
                entries.Insert(Math.Clamp(request.Position, 0, entries.Count), new Entry(item, null));
                Rebuild(schedule, entries);
                return null;
            }

            DbScheduleLane? to = schedule.Blocks.Where(IsSplit).SelectMany(b => b.Lanes)
                .FirstOrDefault(l => l.Id == request.LaneId);

            if (to == null)
                return BasicResponse.WithError(ScheduleNotFound);

            from.Items.Remove(item);
            item.LaneId = to.Id;
            to.Items.Insert(Math.Clamp(request.Position, 0, to.Items.Count), item);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
