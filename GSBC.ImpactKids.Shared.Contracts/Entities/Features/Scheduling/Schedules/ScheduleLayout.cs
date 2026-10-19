using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

/// <summary>One entry of the night's main list: an item everyone does together, or a whole split.</summary>
public sealed record ScheduleEntry(ScheduleItem? Item, ScheduleBlock? Split);

/// <summary>
/// How the stored blocks map to what a leader sees: <b>one list</b>, in which a split is an entry like
/// any item and can be dragged between them.
/// <para>
/// Stored, the night is still blocks - a run of together items is a one-lane block, a split a block of
/// several. The two views are kept in step by one rule, applied after every edit: <b>together blocks
/// only ever sit between splits</b>. Two together blocks side by side are merged, an empty one is
/// dropped, and moving a split between two items cuts their block in two around it. The server applies
/// it to the database in <c>ScheduleService.Normalise</c>; this is the same rule over the contract, for
/// the phone's instant copy of a drag.
/// </para>
/// </summary>
public static class ScheduleLayout
{
    public static ImmutableList<ScheduleEntry> Entries(Schedule schedule) =>
        schedule.Blocks
            .SelectMany(block => block.IsSplit
                ? [new ScheduleEntry(null, block)]
                : block.Lanes.SelectMany(l => l.Items).Select(item => new ScheduleEntry(item, null)))
            .ToImmutableList();

    /// <summary>The schedule with its blocks rebuilt from <paramref name="entries"/>. New together blocks get fresh ids.</summary>
    public static Schedule Build(Schedule schedule, IEnumerable<ScheduleEntry> entries)
    {
        Queue<ScheduleBlock> together = new(schedule.Blocks.Where(b => !b.IsSplit && b.Lanes.Count == 1));

        List<ScheduleBlock>  blocks  = [];
        List<ScheduleItem>?  run     = null;
        ScheduleBlock?       runFrom = null;

        void CloseRun()
        {
            if (run == null)
                return;

            ScheduleBlock block = runFrom ?? new ScheduleBlock { Id = Guid.NewGuid(), Lanes = [new ScheduleLane { Id = Guid.NewGuid() }] };
            blocks.Add(block with { Lanes = [block.Lanes[0] with { Items = run.ToImmutableList() }] });
            run = null;
        }

        foreach (ScheduleEntry entry in entries)
        {
            if (entry.Split != null)
            {
                CloseRun();
                blocks.Add(entry.Split);
            }
            else if (entry.Item != null)
            {
                if (run == null)
                {
                    run = [];
                    runFrom = together.Count > 0 ? together.Dequeue() : null;
                }

                run.Add(entry.Item);
            }
        }

        CloseRun();

        return schedule with { Blocks = blocks.ToImmutableList() };
    }
}
