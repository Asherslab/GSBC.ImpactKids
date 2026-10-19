using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    public async Task<BasicResponse> MoveBlock(MoveScheduleBlockRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfBlock(request.BlockId, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            DbScheduleBlock block = schedule.Blocks.Single(b => b.Id == request.BlockId);

            schedule.Blocks.Remove(block);
            schedule.Blocks.Insert(Math.Clamp(request.Position, 0, schedule.Blocks.Count), block);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
