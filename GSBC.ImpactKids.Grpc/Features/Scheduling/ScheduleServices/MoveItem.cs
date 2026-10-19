using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// A drag: under a section, into a split's lane at a position among its items, or into the main
    /// list at a position among its entries - where a split counts as one. A section never goes under
    /// another section.
    /// </summary>
    public async Task<BasicResponse> MoveItem(MoveScheduleItemRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfItem(request.ItemId, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            if (Locate(schedule, request.ItemId) is not var (from, item))
                return BasicResponse.WithError(ScheduleItemNotFound);

            if (request.SectionId != null)
            {
                if (item.IsSection)
                    return BasicResponse.WithError(ScheduleSectionInSection);

                DbScheduleItem? section = FindItem(schedule, request.SectionId.Value);

                if (section is not { IsSection: true })
                    return BasicResponse.WithError(ScheduleItemNotFound);

                from.Remove(item);
                item.LaneId = null;
                item.ParentItemId = section.Id;
                section.Children.Insert(Math.Clamp(request.Position, 0, section.Children.Count), item);
                return null;
            }

            if (request.LaneId == null)
            {
                from.Remove(item);
                item.ParentItemId = null;

                List<Entry> entries = Entries(schedule);
                entries.Insert(Math.Clamp(request.Position, 0, entries.Count), new Entry(item, null));
                Rebuild(schedule, entries);
                return null;
            }

            DbScheduleLane? to = schedule.Blocks.Where(IsSplit).SelectMany(b => b.Lanes)
                .FirstOrDefault(l => l.Id == request.LaneId);

            if (to == null)
                return BasicResponse.WithError(ScheduleNotFound);

            from.Remove(item);
            item.ParentItemId = null;
            item.LaneId = to.Id;
            to.Items.Insert(Math.Clamp(request.Position, 0, to.Items.Count), item);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
