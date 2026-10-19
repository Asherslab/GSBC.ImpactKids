using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

/// <summary>
/// Gives a service its own copy of a template. A copy, never a link: a template edited later must not
/// rewrite a night that has already happened, or comparing plans against what really ran means nothing.
/// Shared by applying a template and by a new service picking up its service type's default.
/// </summary>
public static class ScheduleCopy
{
    /// <summary>
    /// Replaces whatever schedule the service has with a copy of the template, staged on <paramref name="db"/>
    /// for the caller to save. Returns the new schedule's id, or null when the template does not exist.
    /// </summary>
    public static async Task<Guid?> StageForService(
        GsbcDbContext     db,
        Guid              templateId,
        Guid              serviceId,
        CancellationToken token
    )
    {
        DbSchedule? template = await db.Schedules
            .AsNoTracking()
            .Include(x => x.Groups)
            .Include(x => x.Blocks)
            .ThenInclude(x => x.Lanes)
            .ThenInclude(x => x.Items)
            .ThenInclude(x => x.Children)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == templateId && x.ServiceId == null, token);

        if (template == null)
            return null;

        await db.Schedules.Where(x => x.ServiceId == serviceId).ExecuteDeleteAsync(token);

        Guid                   scheduleId = Guid.NewGuid();
        Dictionary<Guid, Guid> groupIds   = template.Groups.ToDictionary(g => g.Id, _ => Guid.NewGuid());

        DbSchedule copy = new()
        {
            Id = scheduleId,
            ServiceId = serviceId,
            Groups = template.Groups
                .Select(g => new DbScheduleGroup
                {
                    Id = groupIds[g.Id],
                    ScheduleId = scheduleId,
                    Name = g.Name,
                    Position = g.Position
                })
                .ToList(),
            Blocks = template.Blocks
                .Select(b =>
                {
                    Guid blockId = Guid.NewGuid();
                    return new DbScheduleBlock
                    {
                        Id = blockId,
                        ScheduleId = scheduleId,
                        Position = b.Position,
                        Lanes = b.Lanes
                            .Select(l =>
                            {
                                Guid laneId = Guid.NewGuid();
                                return new DbScheduleLane
                                {
                                    Id = laneId,
                                    BlockId = blockId,
                                    Position = l.Position,
                                    GroupIds = l.GroupIds.Where(groupIds.ContainsKey).Select(id => groupIds[id]).ToList(),
                                    OnScreen = l.OnScreen,
                                    Items = l.Items.Select(i => Copy(i, laneId, null)).ToList()
                                };
                            })
                            .ToList()
                    };
                })
                .ToList()
        };

        db.Schedules.Add(copy);

        return scheduleId;
    }

    /// <summary>An item, with the items under it if it is a section. A section's children belong to it, not to a lane.</summary>
    private static DbScheduleItem Copy(DbScheduleItem item, Guid? laneId, Guid? parentId)
    {
        Guid id = Guid.NewGuid();

        return new DbScheduleItem
        {
            Id = id,
            LaneId = laneId,
            ParentItemId = parentId,
            Position = item.Position,
            Name = item.Name,
            FillIn = item.FillIn,
            NeedsFillIn = item.NeedsFillIn,
            IsSection = item.IsSection,
            ItemTemplateId = item.ItemTemplateId,
            PlannedMinutes = item.Minutes(),
            AdjustmentMinutes = 0,
            StartsAt = item.StartsAt,
            Children = item.Children.Select(c => Copy(c, null, id)).ToList()
        };
    }

    private static int Minutes(this DbScheduleItem item) => Math.Max(0, item.PlannedMinutes + item.AdjustmentMinutes);
}
