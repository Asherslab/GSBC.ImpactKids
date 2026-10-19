using System.Text.Json;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// Undoes the last Next on the same track - the stage's, or one lane's - by putting back exactly
    /// what that press changed. Read under the schedule's lock, so two Backs undo two presses.
    /// </summary>
    public Task<BasicResponse> RunBack(RunScheduleRequest request, CallContext context = default) =>
        EditAwaitingAsync(request.ScheduleId, async (schedule, _) =>
        {
            DbScheduleRunStep? last = await db.ScheduleRunSteps
                .Where(x => x.ScheduleId == schedule.Id && x.LaneId == request.LaneId)
                .OrderByDescending(x => x.At)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (last == null)
                return BasicResponse.WithError(ScheduleRunNothingBack);

            RunStepBefore before = JsonSerializer.Deserialize<RunStepBefore>(last.Before)!;

            foreach (RunBefore item in before.Items)
            {
                if (FindItem(schedule, item.ItemId) is not { } found)
                    continue;

                found.ActualStartedAt = item.StartedAt;
                found.ActualEndedAt = item.EndedAt;
            }

            schedule.WaitingItemId = before.WaitingItemId;
            db.ScheduleRunSteps.Remove(last);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
}
