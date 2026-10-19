using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules;

/// <summary>
/// The phone's own copy of a few edits, applied the moment they are made so the screen does not wait
/// for the server. The server applies the real edit under its lock and its answer replaces this one;
/// these only have to be right enough not to jump when it lands.
/// </summary>
public static class ScheduleEdits
{
    public static Schedule MoveItem(Schedule schedule, Guid itemId, Guid laneId, int position)
    {
        ScheduleItem? item = schedule.Items.FirstOrDefault(i => i.Id == itemId);

        if (item == null)
            return schedule;

        return MapLanes(schedule, lane =>
        {
            ImmutableList<ScheduleItem> items = lane.Items.RemoveAll(i => i.Id == itemId);

            if (lane.Id == laneId)
                items = items.Insert(Math.Clamp(position, 0, items.Count), item);

            return lane with { Items = items };
        });
    }

    public static Schedule Adjust(Schedule schedule, Guid itemId, int minutes, bool underway) =>
        MapLanes(schedule, lane => lane with
        {
            Items = lane.Items.Select(i =>
            {
                if (i.Id != itemId)
                    return i;

                int delta = Math.Clamp(i.Minutes + minutes, 0, ScheduleRules.MaxMinutes) - i.Minutes;

                return underway
                    ? i with { AdjustmentMinutes = i.AdjustmentMinutes + delta }
                    : i with { PlannedMinutes = i.PlannedMinutes + delta };
            }).ToImmutableList()
        });

    public static Schedule SetLaneGroups(Schedule schedule, Guid laneId, List<Guid> groupIds) =>
        schedule with
        {
            Blocks = schedule.Blocks.Select(block => block.Lanes.All(l => l.Id != laneId)
                ? block
                : block with
                {
                    Lanes = block.Lanes.Select(l => l.Id == laneId
                        ? l with { GroupIds = groupIds.ToImmutableList() }
                        : l with { GroupIds = l.GroupIds.RemoveAll(groupIds.Contains) }
                    ).ToImmutableList()
                }).ToImmutableList()
        };

    public static Schedule MoveBlock(Schedule schedule, Guid blockId, int position)
    {
        ScheduleBlock? block = schedule.Blocks.FirstOrDefault(b => b.Id == blockId);

        if (block == null)
            return schedule;

        ImmutableList<ScheduleBlock> blocks = schedule.Blocks.Remove(block);

        return schedule with { Blocks = blocks.Insert(Math.Clamp(position, 0, blocks.Count), block) };
    }

    private static Schedule MapLanes(Schedule schedule, Func<ScheduleLane, ScheduleLane> map) =>
        schedule with
        {
            Blocks = schedule.Blocks
                .Select(b => b with { Lanes = b.Lanes.Select(map).ToImmutableList() })
                .ToImmutableList()
        };
}
