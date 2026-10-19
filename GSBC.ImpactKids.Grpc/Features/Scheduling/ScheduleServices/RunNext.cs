using System.Text.Json;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// Next on the night. What one press does is decided by <see cref="ScheduleRun"/> - the same rules
    /// the phone predicts with and the agent reads - and applied here under the schedule's lock, so two
    /// leaders pressing at once move it on twice, in order. Each press is logged with what it changed,
    /// for Back.
    /// </summary>
    public Task<BasicResponse> RunNext(RunScheduleRequest request, CallContext context = default) =>
        EditAsync(request.ScheduleId, (schedule, edit) =>
        {
            if (edit.ServiceDate is not { } serviceDate)
                return BasicResponse.WithError(ScheduleRunNotAService);

            Schedule current = converter.Convert(schedule);
            DateTime now     = DateTime.UtcNow;

            RunPlan? plan = request.LaneId is { } laneId
                ? ScheduleRun.NextInLane(current, laneId, now)
                : ScheduleRun.Next(current, serviceDate, SydneyToUtc, now);

            if (plan == null)
                return BasicResponse.WithError(ScheduleRunNothingNext);

            List<RunBefore> before = [];

            foreach (RunChange change in plan.Changes)
            {
                if (FindItem(schedule, change.ItemId) is not { } item)
                    continue;

                before.Add(new RunBefore(item.Id, item.ActualStartedAt, item.ActualEndedAt));
                item.ActualStartedAt = Utc(change.StartedAt);
                item.ActualEndedAt = Utc(change.EndedAt);
            }

            db.ScheduleRunSteps.Add(new DbScheduleRunStep
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                LaneId = request.LaneId,
                At = now,
                UserId = edit.UserId,
                Before = JsonSerializer.Serialize(new RunStepBefore(before, schedule.WaitingItemId))
            });

            schedule.WaitingItemId = plan.WaitingItemId;
            return null;
        }, context.ServerCallContext, context.CancellationToken);

    /// <summary>What a press changed, as it was before it - stored with the press for Back.</summary>
    private sealed record RunBefore(Guid ItemId, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt);

    private sealed record RunStepBefore(List<RunBefore> Items, Guid? WaitingItemId);

    private static DateTimeOffset? Utc(DateTime? value) =>
        value is { } utc ? new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)) : null;
}
