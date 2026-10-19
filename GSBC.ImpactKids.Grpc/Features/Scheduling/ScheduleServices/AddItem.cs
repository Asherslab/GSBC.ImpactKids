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

        string name      = (template?.Name ?? request.Name ?? "").Trim();
        bool   isSection = template?.IsSection ?? request.IsSection;
        bool   under     = request.SectionId != null;

        // Under a section an item has no length that counts - the section holds the time.
        int minutes = under ? 0 : template?.Minutes ?? request.Minutes;

        if (name.Length == 0)
            return BasicResponse.WithError(ScheduleItemNameNull);

        if (!ScheduleRules.MinutesValid(minutes))
            return BasicResponse.WithError(ScheduleItemMinutesInvalid);

        if (under && isSection)
            return BasicResponse.WithError(ScheduleSectionInSection);

        Guid? scheduleId = request.SectionId is { } sectionId
            ? await ScheduleOfItem(sectionId, token)
            : request.LaneId is { } laneId
                ? await ScheduleOfLane(laneId, token)
                : request.ScheduleId;

        return await EditAsync(scheduleId, (schedule, edit) =>
        {
            if (under)
            {
                DbScheduleItem? section = FindItem(schedule, request.SectionId!.Value);

                if (section is not { IsSection: true })
                    return BasicResponse.WithError(ScheduleItemNotFound);

                DbScheduleItem child = new()
                {
                    Id = Guid.NewGuid(),
                    ParentItemId = section.Id,
                    Position = 0,
                    Name = name,
                    NeedsFillIn = template?.NeedsFillIn ?? false,
                    ItemTemplateId = template?.Id,
                    PlannedMinutes = 0,
                    AdjustmentMinutes = 0
                };

                section.Children.Insert(Math.Clamp(request.Position ?? section.Children.Count, 0, section.Children.Count), child);
                db.ScheduleItems.Add(child);
                return null;
            }

            DbScheduleLane? lane = null;

            if (request.LaneId != null)
            {
                lane = schedule.Blocks.SelectMany(b => b.Lanes).FirstOrDefault(l => l.Id == request.LaneId);

                if (lane == null)
                    return BasicResponse.WithError(ScheduleNotFound);
            }

            DbScheduleItem item = new()
            {
                Id = Guid.NewGuid(),
                LaneId = lane?.Id, // set by Rebuild when it goes into the main list
                Position = 0,
                Name = name,
                IsSection = isSection,
                NeedsFillIn = template?.NeedsFillIn ?? false,
                ItemTemplateId = template?.Id,
                PlannedMinutes = edit.Underway ? 0 : minutes,
                AdjustmentMinutes = edit.Underway ? minutes : 0,
                StartsAt = template?.StartsAt
            };

            if (lane != null)
            {
                lane.Items.Insert(Math.Clamp(request.Position ?? lane.Items.Count, 0, lane.Items.Count), item);
            }
            else
            {
                List<Entry> entries = Entries(schedule);
                entries.Insert(Math.Clamp(request.Position ?? entries.Count, 0, entries.Count), new Entry(item, null));
                Rebuild(schedule, entries);
            }

            db.ScheduleItems.Add(item);

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
