using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// Another lane on the end of a block - which turns a together block into a split. The new lane
    /// takes the first group no other lane of the block covers yet, if there is one.
    /// </summary>
    public async Task<BasicResponse> AddLane(BasicReadRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfBlock(request.Guid, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            DbScheduleBlock block = schedule.Blocks.Single(b => b.Id == request.Guid);

            if (block.Lanes.Count >= ScheduleRules.MaxLanes)
                return BasicResponse.WithError(ScheduleBlockLanesInvalid);

            // Going from together to a split: the lane that was everybody becomes the first group's.
            if (block.Lanes.Count == 1 && block.Lanes[0].GroupIds.Count == 0 && schedule.Groups.Count > 0)
                block.Lanes[0].GroupIds = [schedule.Groups[0].Id];

            HashSet<Guid> covered = block.Lanes.SelectMany(l => l.GroupIds).ToHashSet();

            DbScheduleLane lane = NewLane(block.Id, block.Lanes.Count);

            DbScheduleGroup? free = schedule.Groups.FirstOrDefault(g => !covered.Contains(g.Id));
            if (free != null)
                lane.GroupIds = [free.Id];

            db.ScheduleLanes.Add(lane);
            block.Lanes.Add(lane);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
