using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

namespace GSBC.ImpactKids.Grpc.Tests.Scheduling;

/// <summary>
/// The main list a leader drags in, against the blocks it is stored as. The server applies the same
/// rule to the database after every edit (ScheduleService.Rebuild).
/// </summary>
public class ScheduleLayoutTests
{
    private static ScheduleItem Item(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        NeedsFillIn = false,
        PlannedMinutes = 5,
        AdjustmentMinutes = 0
    };

    private static ScheduleBlock Together(params ScheduleItem[] items) => new()
    {
        Id = Guid.NewGuid(),
        Lanes = [new ScheduleLane { Id = Guid.NewGuid(), Items = items.ToImmutableList() }]
    };

    private static ScheduleBlock Split() => new()
    {
        Id = Guid.NewGuid(),
        Lanes = [new ScheduleLane { Id = Guid.NewGuid() }, new ScheduleLane { Id = Guid.NewGuid() }]
    };

    private static Schedule Of(params ScheduleBlock[] blocks) => new() { Id = Guid.NewGuid(), Blocks = blocks.ToImmutableList() };

    [Fact]
    public void A_split_is_one_entry_among_the_items()
    {
        ScheduleItem  a     = Item("Start"), b = Item("Songs"), c = Item("Pack up");
        ScheduleBlock split = Split();

        ImmutableList<ScheduleEntry> entries = ScheduleLayout.Entries(Of(Together(a, b), split, Together(c)));

        Assert.Equal([a, b, null, c], entries.Select(e => e.Item));
        Assert.Same(split, entries[2].Split);
    }

    [Fact]
    public void Moving_a_split_between_two_items_cuts_their_block_around_it()
    {
        ScheduleItem  a     = Item("Start"), b = Item("Songs");
        ScheduleBlock split = Split();

        Schedule schedule = Of(Together(a, b), split);
        Schedule moved    = ScheduleLayout.Build(schedule, [new(a, null), new(null, split), new(b, null)]);

        Assert.Equal(3, moved.Blocks.Count);
        Assert.Equal([a], moved.Blocks[0].Lanes[0].Items);
        Assert.Same(split, moved.Blocks[1]);
        Assert.Equal([b], moved.Blocks[2].Lanes[0].Items);
    }

    [Fact]
    public void Together_blocks_left_side_by_side_merge_and_empty_ones_go()
    {
        ScheduleItem a = Item("Start"), b = Item("Songs");

        Schedule schedule = Of(Together(a), Together(), Together(b));
        Schedule tidied   = ScheduleLayout.Build(schedule, ScheduleLayout.Entries(schedule));

        ScheduleBlock only = Assert.Single(tidied.Blocks);
        Assert.Equal([a, b], only.Lanes[0].Items);
    }
}
