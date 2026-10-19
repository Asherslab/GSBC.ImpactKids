using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>A lane and its items. Taking a split down to one lane makes it a together block again.</summary>
    public async Task<BasicResponse> RemoveLane(BasicReadRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfLane(request.Guid, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            DbScheduleBlock block = schedule.Blocks.Single(b => b.Lanes.Any(l => l.Id == request.Guid));

            if (block.Lanes.Count == 1)
                return BasicResponse.WithError(ScheduleLastLane);

            DbScheduleLane lane = block.Lanes.Single(l => l.Id == request.Guid);

            block.Lanes.Remove(lane);
            db.ScheduleLanes.Remove(lane);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
