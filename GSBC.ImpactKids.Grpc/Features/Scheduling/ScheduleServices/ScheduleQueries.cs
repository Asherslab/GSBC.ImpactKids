using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public static class ScheduleQueries
{
    /// <summary>
    /// Schedules with their whole tree - groups, blocks, lanes, items, and the items under sections -
    /// which is what <c>ScheduleConverter</c> needs. Read by the schedule service and by the agent's
    /// "tonight".
    /// </summary>
    public static IQueryable<DbSchedule> WholeSchedules(this GsbcDbContext db) =>
        db.Schedules
            .Include(x => x.Groups)
            .Include(x => x.Blocks)
            .ThenInclude(x => x.Lanes)
            .ThenInclude(x => x.Items)
            .ThenInclude(x => x.Children)
            .AsSplitQuery();
}
