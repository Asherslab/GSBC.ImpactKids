using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

/// <summary>Where one item falls on the night's clock. Start and end are null until something before it is pinned to a time.</summary>
public sealed record TimedItem(
    ScheduleItem Item,
    Guid         BlockId,
    Guid         LaneId,
    DateTime?    Start,
    DateTime?    End,
    bool         Clash
);

/// <summary>Where a lane in a split finishes against the longest lane: how much it has to spare.</summary>
public sealed record LaneTiming(Guid LaneId, int Minutes, int SpareMinutes);

/// <summary>
/// The clock times of a schedule, worked out one way for the phone, the server and the agent.
/// <list type="bullet">
/// <item>An item with a start time starts then. If what comes before runs past that time, the item is
/// marked <see cref="TimedItem.Clash"/> rather than anything being squeezed to fit.</item>
/// <item>Otherwise it starts when the item before it in its lane ends.</item>
/// <item>Every lane of a block starts when the block before ends; a block ends when its longest lane does.</item>
/// <item>Items before the first time anything is pinned to have no clock time, only a length.</item>
/// </list>
/// All times are local wall-clock times on <c>localDate</c>.
/// </summary>
public static class ScheduleTimeline
{
    public static ImmutableDictionary<Guid, TimedItem> Compute(Schedule schedule, DateTime localDate)
    {
        DateTime day = localDate.Date;

        ImmutableDictionary<Guid, TimedItem>.Builder timed = ImmutableDictionary.CreateBuilder<Guid, TimedItem>();

        DateTime? cursor = null;

        foreach (ScheduleBlock block in schedule.Blocks)
        {
            DateTime? blockEnd = null;

            foreach (ScheduleLane lane in block.Lanes)
            {
                DateTime? laneCursor = cursor;

                foreach (ScheduleItem item in lane.Items)
                {
                    DateTime? start = laneCursor;
                    bool      clash = false;

                    if (item.StartsAt is { } startsAt)
                    {
                        DateTime pinned = day + startsAt;
                        clash = laneCursor > pinned;
                        start = pinned;
                    }

                    DateTime? end = start?.AddMinutes(item.Minutes);

                    timed[item.Id] = new TimedItem(item, block.Id, lane.Id, start, end, clash);
                    laneCursor = end;
                }

                if (laneCursor != null && (blockEnd == null || laneCursor > blockEnd))
                    blockEnd = laneCursor;
            }

            // A block whose lanes have no items does not move the clock.
            if (blockEnd != null)
                cursor = blockEnd;
        }

        return timed.ToImmutable();
    }

    /// <summary>Minutes per lane of a split, and how far short of the longest lane each finishes.</summary>
    public static ImmutableList<LaneTiming> Lanes(ScheduleBlock block)
    {
        List<(Guid Id, int Minutes)> lengths = block.Lanes.Select(l => (l.Id, l.Items.Sum(i => i.Minutes))).ToList();
        int longest = lengths.Count == 0 ? 0 : lengths.Max(l => l.Minutes);

        return lengths.Select(l => new LaneTiming(l.Id, l.Minutes, longest - l.Minutes)).ToImmutableList();
    }

    /// <summary>The total length of the night: blocks end to end, each as long as its longest lane.</summary>
    public static int TotalMinutes(Schedule schedule) =>
        schedule.Blocks.Sum(b => b.Lanes.Count == 0 ? 0 : b.Lanes.Max(l => l.Items.Sum(i => i.Minutes)));

    /// <summary>
    /// Whether the night has started, which is when a change of length stops being planning and
    /// becomes an adjustment: once the first item has finished. With no clock time to go by, the night
    /// is taken to have started from the service's day onwards.
    /// </summary>
    public static bool IsUnderway(Schedule schedule, DateTime localServiceDate, DateTime localNow)
    {
        ScheduleItem? first = schedule.Items.FirstOrDefault();

        if (first != null)
        {
            ImmutableDictionary<Guid, TimedItem> timed = Compute(schedule, localServiceDate);

            if (timed.TryGetValue(first.Id, out TimedItem? firstTimed) && firstTimed.End is { } end)
                return localNow >= end;
        }

        return localNow.Date >= localServiceDate.Date;
    }
}
