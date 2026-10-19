using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>Another lane on the end of a split. It takes the first group no other lane of the split covers yet, if there is one.</summary>
    public async Task<BasicResponse> AddLane(BasicReadRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfBlock(request.Guid, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            DbScheduleBlock? block = schedule.Blocks.FirstOrDefault(b => b.Id == request.Guid && IsSplit(b));

            if (block == null)
                return BasicResponse.WithError(ScheduleNotFound);

            if (block.Lanes.Count >= ScheduleRules.MaxLanes)
                return BasicResponse.WithError(ScheduleBlockLanesInvalid);

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
