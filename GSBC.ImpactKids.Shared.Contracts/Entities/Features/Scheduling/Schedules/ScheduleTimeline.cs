using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

/// <summary>
/// Where one item falls on the night's clock. Start and end are null until something before it is
/// pinned to a time. <see cref="Fills"/> marks a 0-minute item stretched to the next fixed point.
/// </summary>
public sealed record TimedItem(
    ScheduleItem Item,
    Guid         BlockId,
    Guid         LaneId,
    DateTime?    Start,
    DateTime?    End,
    bool         Clash,
    bool         Fills = false
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
/// <item><b>A 0-minute item fills.</b> Games is planned at 0 because it lasts as long as the lesson
/// in the other lane. So a 0-minute item that ends a lane of a split runs to the end of the split, and
/// one followed by an item with a start time runs up to that time.</item>
/// </list>
/// Items under a section are not on the clock; the section holds the time.
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
                DateTime?     laneCursor = cursor;
                ScheduleItem? previous   = null;

                foreach (ScheduleItem item in lane.Items)
                {
                    DateTime? start = laneCursor;
                    bool      clash = false;

                    if (item.StartsAt is { } startsAt)
                    {
                        DateTime pinned = day + startsAt;
                        clash = laneCursor > pinned;
                        start = pinned;

                        // A 0-minute item just before a start time fills the gap up to it.
                        if (previous is { Minutes: 0 } && timed[previous.Id] is { End: { } previousEnd } before && previousEnd < pinned)
                            timed[previous.Id] = before with { End = pinned, Fills = true };
                    }

                    DateTime? end = start?.AddMinutes(item.Minutes);

                    timed[item.Id] = new TimedItem(item, block.Id, lane.Id, start, end, clash);
                    laneCursor = end;
                    previous = item;
                }

                if (laneCursor != null && (blockEnd == null || laneCursor > blockEnd))
                    blockEnd = laneCursor;
            }

            // A 0-minute item ending a lane of a split runs on until the split ends.
            if (block.IsSplit && blockEnd != null)
            {
                foreach (ScheduleLane lane in block.Lanes)
                {
                    if (lane.Items.LastOrDefault() is { Minutes: 0 } last
                        && timed[last.Id] is { End: { } lastEnd } lastTimed
                        && lastEnd < blockEnd)
                        timed[last.Id] = lastTimed with { End = blockEnd, Fills = true };
                }
            }

            // A block whose lanes have no items does not move the clock.
            if (blockEnd != null)
                cursor = blockEnd;
        }

        return timed.ToImmutable();
    }

    /// <summary>
    /// Minutes per lane of a split, and how far short of the longest lane each finishes. A lane ending
    /// in a 0-minute item has nothing to spare: that item fills it.
    /// </summary>
    public static ImmutableList<LaneTiming> Lanes(ScheduleBlock block)
    {
        int longest = block.Lanes.Count == 0 ? 0 : block.Lanes.Max(l => l.Items.Sum(i => i.Minutes));

        return block.Lanes
            .Select(l =>
            {
                int  minutes = l.Items.Sum(i => i.Minutes);
                bool fills   = l.Items.LastOrDefault() is { Minutes: 0 };
                return new LaneTiming(l.Id, fills ? longest : minutes, fills ? 0 : longest - minutes);
            })
            .ToImmutableList();
    }

    /// <summary>
    /// The length of the night. With clock times, it is first start to last finish - which counts what
    /// a sum of lengths cannot: a 0-minute item filling up to a fixed time, and any gap before one.
    /// With none, it is the blocks end to end, each as long as its longest lane.
    /// </summary>
    public static int TotalMinutes(Schedule schedule)
    {
        // Any day will do: only the span between the times is wanted.
        ImmutableDictionary<Guid, TimedItem> timed = Compute(schedule, new DateTime(2000, 1, 1));

        DateTime? start = timed.Values.Where(t => t.Start != null).Select(t => t.Start).Min();
        DateTime? end   = timed.Values.Where(t => t.End != null).Select(t => t.End).Max();

        if (start != null && end != null)
            return (int)(end.Value - start.Value).TotalMinutes;

        return schedule.Blocks.Sum(b => b.Lanes.Count == 0 ? 0 : b.Lanes.Max(l => l.Items.Sum(i => i.Minutes)));
    }

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
