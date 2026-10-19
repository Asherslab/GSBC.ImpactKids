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

    private IQueryable<DbSchedule> WholeSchedules => db.Schedules
        .Include(x => x.Groups)
        .Include(x => x.Blocks)
        .ThenInclude(x => x.Lanes)
        .ThenInclude(x => x.Items)
        .AsSplitQuery();

    /// <summary>What an edit may want besides the tree: whether the night has started, and who is editing.</summary>
    private sealed record EditContext(bool Underway, Guid? UserId);

    private async Task<BasicResponse> EditAsync(
        Guid?                                          scheduleId,
        Func<DbSchedule, EditContext, BasicResponse?> edit,
        ServerCallContext?                             call,
        CancellationToken                              token
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

        EditContext context = new(await IsUnderwayAsync(schedule, token), UserIdOf(call));

        BasicResponse? refused = edit(schedule, context);

        if (refused != null)
            return refused;

        Renumber(schedule);

        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse { Success = true };
    }

    // New rows are handed to db.Add as well as put in their list. A row with its Guid already set,
    // found only through a navigation, is taken by EF for an existing row and UPDATEd - which then
    // matches nothing and throws. Removed rows go through db.Remove for the same clarity.

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
                    lane.Items[i].Position = i;
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
                lane.Items.Sort((a, b) => a.Position.CompareTo(b.Position));
        }
    }

    private async Task<bool> IsUnderwayAsync(DbSchedule schedule, CancellationToken token)
    {
        if (schedule.ServiceId == null)
            return false;

        DateTimeOffset? serviceDate = await db.Services
            .Where(x => x.Id == schedule.ServiceId)
            .Select(x => (DateTimeOffset?)x.Date)
            .FirstOrDefaultAsync(token);

        if (serviceDate == null)
            return false;

        DateTime localServiceDate = TimeZoneInfo.ConvertTime(serviceDate.Value, SydneyZone).Date;
        DateTime localNow         = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, SydneyZone).DateTime;

        return ScheduleTimeline.IsUnderway(converter.Convert(schedule), localServiceDate, localNow);
    }

    private static Guid? UserIdOf(ServerCallContext? call) =>
        Guid.TryParse(call?.GetHttpContext().User.FindFirstValue("UserId"), out Guid id) ? id : null;

    private Task<Guid?> ScheduleOfBlock(Guid blockId, CancellationToken token) =>
        db.ScheduleBlocks.Where(x => x.Id == blockId).Select(x => (Guid?)x.ScheduleId).FirstOrDefaultAsync(token);

    private Task<Guid?> ScheduleOfLane(Guid laneId, CancellationToken token) =>
        db.ScheduleLanes.Where(x => x.Id == laneId).Select(x => (Guid?)x.Block!.ScheduleId).FirstOrDefaultAsync(token);

    private Task<Guid?> ScheduleOfItem(Guid itemId, CancellationToken token) =>
        db.ScheduleItems.Where(x => x.Id == itemId).Select(x => (Guid?)x.Lane!.Block!.ScheduleId).FirstOrDefaultAsync(token);

    private Task<Guid?> ScheduleOfGroup(Guid groupId, CancellationToken token) =>
        db.ScheduleGroups.Where(x => x.Id == groupId).Select(x => (Guid?)x.ScheduleId).FirstOrDefaultAsync(token);

    private static DbScheduleLane NewLane(Guid blockId, int position) => new()
    {
        Id = Guid.NewGuid(),
        BlockId = blockId,
        Position = position
    };
}
