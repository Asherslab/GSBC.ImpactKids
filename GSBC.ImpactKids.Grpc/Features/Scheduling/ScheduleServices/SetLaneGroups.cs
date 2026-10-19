using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// Which groups a lane covers. A group belongs to one lane per block, so giving it to this lane
    /// takes it off its sibling - the feed for that group has to resolve to exactly one lane.
    /// </summary>
    public async Task<BasicResponse> SetLaneGroups(SetScheduleLaneGroupsRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfLane(request.LaneId, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            HashSet<Guid> known  = schedule.Groups.Select(g => g.Id).ToHashSet();
            List<Guid>    wanted = request.GroupIds.Where(known.Contains).Distinct().ToList();

            DbScheduleBlock block = schedule.Blocks.Single(b => b.Lanes.Any(l => l.Id == request.LaneId));

            foreach (DbScheduleLane lane in block.Lanes)
            {
                if (lane.Id == request.LaneId)
                    lane.GroupIds = wanted;
                else if (lane.GroupIds.Any(wanted.Contains))
                    lane.GroupIds = lane.GroupIds.Where(id => !wanted.Contains(id)).ToList();
            }

            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
