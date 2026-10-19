using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>The block and everything in it. The phone confirms first when it holds items.</summary>
    public async Task<BasicResponse> RemoveBlock(BasicReadRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfBlock(request.Guid, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            DbScheduleBlock block = schedule.Blocks.Single(b => b.Id == request.Guid);

            schedule.Blocks.Remove(block);
            db.ScheduleBlocks.Remove(block);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
