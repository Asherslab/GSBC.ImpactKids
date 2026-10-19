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
    /// <summary>Into a split's lane at a position, or into the main list (<paramref name="laneId"/> null) at an entry position.</summary>
    public static Schedule MoveItem(Schedule schedule, Guid itemId, Guid? laneId, int position)
    {
        ScheduleItem? item = schedule.Items.FirstOrDefault(i => i.Id == itemId);

        if (item == null)
            return schedule;

        Schedule without = MapLanes(schedule, lane => lane with { Items = lane.Items.RemoveAll(i => i.Id == itemId) });

        if (laneId == null)
        {
            ImmutableList<ScheduleEntry> entries = ScheduleLayout.Entries(without);
            return ScheduleLayout.Build(without,
                entries.Insert(Math.Clamp(position, 0, entries.Count), new ScheduleEntry(item, null)));
        }

        Schedule moved = MapLanes(without, lane => lane.Id != laneId
            ? lane
            : lane with { Items = lane.Items.Insert(Math.Clamp(position, 0, lane.Items.Count), item) });

        // Taking the item out may have emptied a together run; tidy the same way the server will.
        return ScheduleLayout.Build(moved, ScheduleLayout.Entries(moved));
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

    /// <summary>A split to a new entry position in the main list.</summary>
    public static Schedule MoveBlock(Schedule schedule, Guid blockId, int position)
    {
        ImmutableList<ScheduleEntry> entries = ScheduleLayout.Entries(schedule);
        ScheduleEntry?               split   = entries.FirstOrDefault(e => e.Split?.Id == blockId);

        if (split == null)
            return schedule;

        entries = entries.Remove(split);

        return ScheduleLayout.Build(schedule, entries.Insert(Math.Clamp(position, 0, entries.Count), split));
    }

    private static Schedule MapLanes(Schedule schedule, Func<ScheduleLane, ScheduleLane> map) =>
        schedule with
        {
            Blocks = schedule.Blocks
                .Select(b => b with { Lanes = b.Lanes.Select(map).ToImmutableList() })
                .ToImmutableList()
        };
}
