using System.Security.Claims;
using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

/// <summary>
/// Templates and services' schedules, edited one small operation at a time.
/// <para>
/// <b>Every edit goes through <see cref="EditAsync"/>.</b> It locks the schedule's row for the length
/// of the transaction, loads the whole tree, applies the edit and renumbers every position. Two leaders
/// dragging on two phones at the same moment are applied one after the other against the latest tree,
/// so the order cannot come out interleaved, and positions never collide or gap.
/// </para>
/// </summary>
public partial class ScheduleService(
    GsbcDbContext                    db,
    IEventService<Schedule>          eventService,
    IConverter<DbSchedule, Schedule> converter
) : IScheduleService
{
    private static readonly TimeZoneInfo SydneyZone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    private IQueryable<DbSchedule> WholeSchedules => db.WholeSchedules();

    /// <summary>
    /// What an edit may want besides the tree: whether the night has started, who is editing, and the
    /// service's local date - null for a template.
    /// </summary>
    private sealed record EditContext(bool Underway, Guid? UserId, DateTime? ServiceDate);

    private Task<BasicResponse> EditAsync(
        Guid?                                          scheduleId,
        Func<DbSchedule, EditContext, BasicResponse?> edit,
        ServerCallContext?                             call,
        CancellationToken                              token
    ) =>
        EditAwaitingAsync(scheduleId, (schedule, context) => Task.FromResult(edit(schedule, context)), call, token);

    /// <summary>The same, for an edit that has to query the database itself while it holds the lock.</summary>
    private async Task<BasicResponse> EditAwaitingAsync(
        Guid?                                                scheduleId,
        Func<DbSchedule, EditContext, Task<BasicResponse?>> edit,
        ServerCallContext?                                   call,
        CancellationToken                                    token
    )
    {
        if (scheduleId == null)
            return BasicResponse.WithError(ScheduleNotFound);

        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(token);

        // The lock. Released by the commit, or by the rollback when the transaction is disposed.
        bool exists = await db.Schedules
            .FromSqlInterpolated($"""SELECT * FROM "Schedules" WHERE "Id" = {scheduleId} FOR UPDATE""")
            .AnyAsync(token);

        if (!exists)
            return BasicResponse.WithError(ScheduleNotFound);

        DbSchedule schedule = await WholeSchedules.FirstAsync(x => x.Id == scheduleId, token);
        SortByPosition(schedule);

        DateTime?   serviceDate = await ServiceDateAsync(schedule, token);
        EditContext context     = new(IsUnderway(schedule, serviceDate), UserIdOf(call), serviceDate);

        BasicResponse? refused = await edit(schedule, context);

        if (refused != null)
            return refused;

        Rebuild(schedule, Entries(schedule));
        Renumber(schedule);

        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse { Success = true };
    }

    // New rows are handed to db.Add as well as put in their list. A row with its Guid already set,
    // found only through a navigation, is taken by EF for an existing row and UPDATEd - which then
    // matches nothing and throws. Removed rows go through db.Remove for the same clarity.

    /// <summary>One entry of the main list: an item everyone does, or a whole split. See <see cref="ScheduleLayout"/>.</summary>
    private sealed record Entry(DbScheduleItem? Item, DbScheduleBlock? Split);

    private static bool IsSplit(DbScheduleBlock block) => block.Lanes.Count > 1;

    /// <summary>The main list as a leader sees it: together items one by one, each split as one entry.</summary>
    private static List<Entry> Entries(DbSchedule schedule) =>
        schedule.Blocks
            .SelectMany(block => IsSplit(block)
                ? [new Entry(null, block)]
                : block.Lanes.SelectMany(l => l.Items).Select(item => new Entry(item, null)))
            .ToList();

    /// <summary>
    /// Re-lays the blocks to match <paramref name="entries"/>: each run of together items in one
    /// together block, broken only by splits. Run after every edit, it is also what merges two together
    /// blocks left side by side and drops one left empty - the rule in <see cref="ScheduleLayout"/>.
    /// Together blocks already stored are reused in order, so a plain reorder creates no rows.
    /// </summary>
    private void Rebuild(DbSchedule schedule, List<Entry> entries)
    {
        List<DbScheduleBlock> together = schedule.Blocks.Where(b => !IsSplit(b)).ToList();

        // Every together item is about to be placed again from entries, so empty them all first.
        foreach (DbScheduleLane lane in together.SelectMany(b => b.Lanes))
            lane.Items.Clear();

        Queue<DbScheduleBlock> spare  = new(together.Where(b => b.Lanes.Count == 1));
        List<DbScheduleBlock>  blocks = [];
        DbScheduleLane?        run    = null;

        foreach (Entry entry in entries)
        {
            if (entry.Split != null)
            {
                run = null;
                blocks.Add(entry.Split);
                continue;
            }

            if (run == null)
            {
                DbScheduleBlock block = spare.Count > 0 ? spare.Dequeue() : NewTogetherBlock(schedule.Id);
                blocks.Add(block);
                run = block.Lanes[0];
            }

            entry.Item!.LaneId = run.Id;
            run.Items.Add(entry.Item);
        }

        // Together blocks with nothing left to hold, and any stray lane-less block.
        foreach (DbScheduleBlock unused in schedule.Blocks.Where(b => !blocks.Contains(b)).ToList())
            db.ScheduleBlocks.Remove(unused);

        schedule.Blocks.Clear();
        schedule.Blocks.AddRange(blocks);
    }

    private DbScheduleBlock NewTogetherBlock(Guid scheduleId)
    {
        DbScheduleBlock block = new()
        {
            Id = Guid.NewGuid(),
            ScheduleId = scheduleId,
            Position = 0
        };

        DbScheduleLane lane = NewLane(block.Id, 0);
        block.Lanes.Add(lane);

        db.ScheduleBlocks.Add(block);
        db.ScheduleLanes.Add(lane);
        return block;
    }

    /// <summary>Positions 0, 1, 2… in the order the lists are in now. Edits reorder the lists; this makes it stick.</summary>
    private static void Renumber(DbSchedule schedule)
    {
        for (int g = 0; g < schedule.Groups.Count; g++)
            schedule.Groups[g].Position = g;

        for (int b = 0; b < schedule.Blocks.Count; b++)
        {
            DbScheduleBlock block = schedule.Blocks[b];
            block.Position = b;

            for (int l = 0; l < block.Lanes.Count; l++)
            {
                DbScheduleLane lane = block.Lanes[l];
                lane.Position = l;

                for (int i = 0; i < lane.Items.Count; i++)
                {
                    lane.Items[i].Position = i;

                    for (int c = 0; c < lane.Items[i].Children.Count; c++)
                        lane.Items[i].Children[c].Position = c;
                }
            }
        }
    }

    /// <summary>Sorts every list by its stored position, so an edit can insert and remove by index.</summary>
    private static void SortByPosition(DbSchedule schedule)
    {
        schedule.Groups.Sort((a, b) => a.Position.CompareTo(b.Position));
        schedule.Blocks.Sort((a, b) => a.Position.CompareTo(b.Position));

        foreach (DbScheduleBlock block in schedule.Blocks)
        {
            block.Lanes.Sort((a, b) => a.Position.CompareTo(b.Position));

            foreach (DbScheduleLane lane in block.Lanes)
            {
                lane.Items.Sort((a, b) => a.Position.CompareTo(b.Position));

                foreach (DbScheduleItem item in lane.Items)
                    item.Children.Sort((a, b) => a.Position.CompareTo(b.Position));
            }
        }
    }

    /// <summary>The service's date in Sydney, which is the day its clock times are on. Null for a template.</summary>
    private async Task<DateTime?> ServiceDateAsync(DbSchedule schedule, CancellationToken token)
    {
        if (schedule.ServiceId == null)
            return null;

        DateTimeOffset? serviceDate = await db.Services
            .Where(x => x.Id == schedule.ServiceId)
            .Select(x => (DateTimeOffset?)x.Date)
            .FirstOrDefaultAsync(token);

        return serviceDate == null ? null : TimeZoneInfo.ConvertTime(serviceDate.Value, SydneyZone).Date;
    }

    private bool IsUnderway(DbSchedule schedule, DateTime? serviceDate)
    {
        if (serviceDate == null)
            return false;

        DateTime localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, SydneyZone).DateTime;

        return ScheduleTimeline.IsUnderway(converter.Convert(schedule), serviceDate.Value, localNow);
    }

    /// <summary>A Sydney wall-clock time as UTC - how a fixed start time becomes an instant.</summary>
    private static DateTime SydneyToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), SydneyZone);

    private static Guid? UserIdOf(ServerCallContext? call) =>
        Guid.TryParse(call?.GetHttpContext().User.FindFirstValue("UserId"), out Guid id) ? id : null;

    private Task<Guid?> ScheduleOfBlock(Guid blockId, CancellationToken token) =>
        db.ScheduleBlocks.Where(x => x.Id == blockId).Select(x => (Guid?)x.ScheduleId).FirstOrDefaultAsync(token);

    private Task<Guid?> ScheduleOfLane(Guid laneId, CancellationToken token) =>
        db.ScheduleLanes.Where(x => x.Id == laneId).Select(x => (Guid?)x.Block!.ScheduleId).FirstOrDefaultAsync(token);

    private Task<Guid?> ScheduleOfItem(Guid itemId, CancellationToken token) =>
        db.ScheduleItems
            .Where(x => x.Id == itemId)
            .Select(x => x.LaneId != null
                ? (Guid?)x.Lane!.Block!.ScheduleId
                : (Guid?)x.ParentItem!.Lane!.Block!.ScheduleId)
            .FirstOrDefaultAsync(token);

    private Task<Guid?> ScheduleOfGroup(Guid groupId, CancellationToken token) =>
        db.ScheduleGroups.Where(x => x.Id == groupId).Select(x => (Guid?)x.ScheduleId).FirstOrDefaultAsync(token);

    private static DbScheduleLane NewLane(Guid blockId, int position) => new()
    {
        Id = Guid.NewGuid(),
        BlockId = blockId,
        Position = position
    };
}
