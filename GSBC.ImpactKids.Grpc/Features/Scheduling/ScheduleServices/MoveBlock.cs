using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>A split dragged to a new place in the main list - between two items cuts their run in two around it.</summary>
    public async Task<BasicResponse> MoveBlock(MoveScheduleBlockRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfBlock(request.BlockId, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            List<Entry> entries = Entries(schedule);
            Entry?      split   = entries.FirstOrDefault(e => e.Split?.Id == request.BlockId);

            if (split == null)
                return BasicResponse.WithError(ScheduleNotFound);

            entries.Remove(split);
            entries.Insert(Math.Clamp(request.Position, 0, entries.Count), split);
            Rebuild(schedule, entries);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
