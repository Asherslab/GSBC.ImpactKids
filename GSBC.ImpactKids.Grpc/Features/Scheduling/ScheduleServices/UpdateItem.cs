using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Grpc.Data.Models.Songs;
using Microsoft.EntityFrameworkCore;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// Name, fill-in (or a song item's song and arrangement), start time and length. The length is the length wanted: before the night it
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

        DbSong? song = null;

        if (request.SongId != null)
        {
            song = await db.Songs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.SongId, context.CancellationToken);

            if (song == null)
                return BasicResponse.WithError(SongNotFound);

            if (request.ArrangementUuid != null
             && SongContent.FromJson(song.ContentJson).Arrangements.All(x => x.Uuid != request.ArrangementUuid))
                return BasicResponse.WithError(SongArrangementNotFound);
        }

        Guid? scheduleId = await ScheduleOfItem(request.ItemId, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, edit) =>
        {
            DbScheduleItem? item = FindItem(schedule, request.ItemId);

            if (item == null)
                return BasicResponse.WithError(ScheduleItemNotFound);

            if (song != null && !item.IsSong)
                return BasicResponse.WithError(ScheduleItemNotASong);

            item.Name = name;
            // A song from the library is named as the library names it; one not in it yet is typed.
            item.FillIn = song?.Name ?? (string.IsNullOrWhiteSpace(request.FillIn) ? null : request.FillIn.Trim());
            item.SongId = song?.Id;
            item.ArrangementUuid = song != null ? request.ArrangementUuid : null;
            item.StartsAt = request.StartsAt;

            ChangeLength(item, request.Minutes - (item.PlannedMinutes + item.AdjustmentMinutes), edit);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }

    private static DbScheduleItem? FindItem(DbSchedule schedule, Guid itemId) => Locate(schedule, itemId)?.Item;

    /// <summary>An item and the list that holds it - its lane's, or its section's.</summary>
    private static (List<DbScheduleItem> List, DbScheduleItem Item)? Locate(DbSchedule schedule, Guid itemId)
    {
        foreach (DbScheduleLane lane in schedule.Blocks.SelectMany(b => b.Lanes))
        {
            foreach (DbScheduleItem top in lane.Items)
            {
                if (top.Id == itemId)
                    return (lane.Items, top);

                DbScheduleItem? child = top.Children.FirstOrDefault(c => c.Id == itemId);
                if (child != null)
                    return (top.Children, child);
            }
        }

        return null;
    }

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
