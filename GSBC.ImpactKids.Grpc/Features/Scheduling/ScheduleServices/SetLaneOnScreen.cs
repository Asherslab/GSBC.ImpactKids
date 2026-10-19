using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// The lane the stage follows during its split. One per split: choosing one un-chooses its
    /// siblings; un-choosing hands the stage back to the longest lane.
    /// </summary>
    public async Task<BasicResponse> SetLaneOnScreen(SetScheduleLaneOnScreenRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfLane(request.LaneId, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            DbScheduleBlock? block = schedule.Blocks.FirstOrDefault(b => b.Lanes.Any(l => l.Id == request.LaneId));

            if (block == null)
                return BasicResponse.WithError(ScheduleNotFound);

            foreach (DbScheduleLane lane in block.Lanes)
                lane.OnScreen = request.OnScreen && lane.Id == request.LaneId;

            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
