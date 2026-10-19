using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// An item from the template library, or a one-off by name. Added once the night is underway, it
    /// was never planned: its planned length is zero and the whole of it is an adjustment, which is what
    /// a later look at "what did we add on the night" needs to see.
    /// </summary>
    public async Task<BasicResponse> AddItem(AddScheduleItemRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbScheduleItemTemplate? template = null;

        if (request.ItemTemplateId != null)
        {
            template = await db.ScheduleItemTemplates.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ItemTemplateId, token);

            if (template == null)
                return BasicResponse.WithError(ScheduleItemTemplateNotFound);
        }

        string name    = (template?.Name ?? request.Name ?? "").Trim();
        int    minutes = template?.Minutes ?? request.Minutes;

        if (name.Length == 0)
            return BasicResponse.WithError(ScheduleItemNameNull);

        if (!ScheduleRules.MinutesValid(minutes))
            return BasicResponse.WithError(ScheduleItemMinutesInvalid);

        Guid? scheduleId = await ScheduleOfLane(request.LaneId, token);

        return await EditAsync(scheduleId, (schedule, edit) =>
        {
            DbScheduleLane? lane = schedule.Blocks.SelectMany(b => b.Lanes).FirstOrDefault(l => l.Id == request.LaneId);

            if (lane == null)
                return BasicResponse.WithError(ScheduleNotFound);

            DbScheduleItem item = new()
            {
                Id = Guid.NewGuid(),
                LaneId = lane.Id,
                Position = 0,
                Name = name,
                NeedsFillIn = template?.NeedsFillIn ?? false,
                ItemTemplateId = template?.Id,
                PlannedMinutes = edit.Underway ? 0 : minutes,
                AdjustmentMinutes = edit.Underway ? minutes : 0,
                StartsAt = template?.StartsAt
            };

            db.ScheduleItems.Add(item);
            lane.Items.Insert(Math.Clamp(request.Position ?? lane.Items.Count, 0, lane.Items.Count), item);

            if (edit.Underway && minutes != 0)
                LogAdjustment(item, minutes, edit);

            return null;
        }, context.ServerCallContext, token);
    }

    private void LogAdjustment(DbScheduleItem item, int minutes, EditContext edit) =>
        db.ScheduleItemAdjustments.Add(new DbScheduleItemAdjustment
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            Minutes = minutes,
            At = DateTimeOffset.UtcNow,
            UserId = edit.UserId
        });
}
