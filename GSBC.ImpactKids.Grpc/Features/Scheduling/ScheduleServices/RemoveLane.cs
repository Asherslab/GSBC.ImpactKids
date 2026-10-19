using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>A lane and its items. A split keeps at least two; below that, remove the split.</summary>
    public async Task<BasicResponse> RemoveLane(BasicReadRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfLane(request.Guid, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            DbScheduleBlock block = schedule.Blocks.Single(b => b.Lanes.Any(l => l.Id == request.Guid));

            // One lane would be a together block, and the next tidy-up would fold it into the main list.
            if (block.Lanes.Count <= 2)
                return BasicResponse.WithError(ScheduleLastLane);

            DbScheduleLane lane = block.Lanes.Single(l => l.Id == request.Guid);

            block.Lanes.Remove(lane);
            db.ScheduleLanes.Remove(lane);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
