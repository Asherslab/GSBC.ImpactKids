using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// Name, fill-in, start time and length. The length is the length wanted: before the night it
    /// replaces the plan, during it the difference goes down as an adjustment - the same rule as +5.
    /// </summary>
    public async Task<BasicResponse> UpdateItem(UpdateScheduleItemRequest request, CallContext context = default)
    {
        string name = request.Name.Trim();

        if (name.Length == 0)
            return BasicResponse.WithError(ScheduleItemNameNull);

        if (!ScheduleRules.MinutesValid(request.Minutes))
            return BasicResponse.WithError(ScheduleItemMinutesInvalid);

        if (!ScheduleRules.StartsAtValid(request.StartsAt))
            return BasicResponse.WithError(ScheduleItemStartsAtInvalid);

        Guid? scheduleId = await ScheduleOfItem(request.ItemId, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, edit) =>
        {
            DbScheduleItem? item = FindItem(schedule, request.ItemId);

            if (item == null)
                return BasicResponse.WithError(ScheduleItemNotFound);

            item.Name = name;
            item.FillIn = string.IsNullOrWhiteSpace(request.FillIn) ? null : request.FillIn.Trim();
            item.StartsAt = request.StartsAt;

            ChangeLength(item, request.Minutes - (item.PlannedMinutes + item.AdjustmentMinutes), edit);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }

    private static DbScheduleItem? FindItem(DbSchedule schedule, Guid itemId) =>
        schedule.Blocks.SelectMany(b => b.Lanes).SelectMany(l => l.Items).FirstOrDefault(i => i.Id == itemId);

    /// <summary>The one place a length changes: into the plan before the night, into the adjustments during it.</summary>
    private void ChangeLength(DbScheduleItem item, int delta, EditContext edit)
    {
        if (delta == 0)
            return;

        if (edit.Underway)
        {
            item.AdjustmentMinutes += delta;
            LogAdjustment(item, delta, edit);
        }
        else
        {
            item.PlannedMinutes += delta;
        }
    }
}
