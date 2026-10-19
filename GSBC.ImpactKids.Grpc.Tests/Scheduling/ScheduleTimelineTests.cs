using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

namespace GSBC.ImpactKids.Grpc.Tests.Scheduling;

/// <summary>
/// The clock times of a schedule. The phone, the server and the ProPresenter agent all read them from
/// <see cref="ScheduleTimeline"/>, so these are the rules the night runs on.
/// </summary>
public class ScheduleTimelineTests
{
    private static readonly DateTime Friday = new(2026, 10, 23);

    private static ScheduleItem Item(string name, int minutes, TimeSpan? startsAt = null, int adjustment = 0) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        NeedsFillIn = false,
        PlannedMinutes = minutes,
        AdjustmentMinutes = adjustment,
        StartsAt = startsAt
    };

    private static ScheduleLane Lane(params ScheduleItem[] items) => new()
    {
        Id = Guid.NewGuid(),
        Items = items.ToImmutableList()
    };

    private static ScheduleBlock Block(params ScheduleLane[] lanes) => new()
    {
        Id = Guid.NewGuid(),
        Lanes = lanes.ToImmutableList()
    };

    private static Schedule Schedule(params ScheduleBlock[] blocks) => new()
    {
        Id = Guid.NewGuid(),
        Blocks = blocks.ToImmutableList()
    };

    private static DateTime At(int hour, int minute) => Friday.AddHours(hour).AddMinutes(minute);

    [Fact]
    public void Items_run_on_from_the_first_start_time()
    {
        ScheduleItem start = Item("Start Night", 5, new TimeSpan(18, 30, 0));
        ScheduleItem songs = Item("Songs", 10);

        ImmutableDictionary<Guid, TimedItem> timed =
            ScheduleTimeline.Compute(Schedule(Block(Lane(start, songs))), Friday);

        Assert.Equal(At(18, 30), timed[start.Id].Start);
        Assert.Equal(At(18, 35), timed[songs.Id].Start);
        Assert.Equal(At(18, 45), timed[songs.Id].End);
    }

    [Fact]
    public void Items_before_any_start_time_have_no_clock_time()
    {
        ScheduleItem loose = Item("Setup", 10);

        ImmutableDictionary<Guid, TimedItem> timed = ScheduleTimeline.Compute(Schedule(Block(Lane(loose))), Friday);

        Assert.Null(timed[loose.Id].Start);
        Assert.Null(timed[loose.Id].End);
    }

    [Fact]
    public void A_split_ends_when_its_longest_lane_does()
    {
        ScheduleItem start   = Item("Start Night", 10, new TimeSpan(18, 30, 0));
        ScheduleItem lesson  = Item("Lesson", 20);
        ScheduleItem games   = Item("Games", 30);
        ScheduleItem packUp  = Item("Pack up", 5);

        Schedule schedule = Schedule(
            Block(Lane(start)),
            Block(Lane(lesson), Lane(games)),
            Block(Lane(packUp))
        );

        ImmutableDictionary<Guid, TimedItem> timed = ScheduleTimeline.Compute(schedule, Friday);

        Assert.Equal(At(18, 40), timed[lesson.Id].Start);
        Assert.Equal(At(18, 40), timed[games.Id].Start);
        Assert.Equal(At(19, 10), timed[packUp.Id].Start);
        Assert.Equal(45, ScheduleTimeline.TotalMinutes(schedule));

        ImmutableList<LaneTiming> lanes = ScheduleTimeline.Lanes(schedule.Blocks[1]);
        Assert.Equal(10, lanes[0].SpareMinutes);
        Assert.Equal(0, lanes[1].SpareMinutes);
    }

    [Fact]
    public void An_overrun_into_a_start_time_is_a_clash_not_a_squeeze()
    {
        ScheduleItem start  = Item("Start Night", 40, new TimeSpan(18, 30, 0));
        ScheduleItem dinner = Item("Dinner", 15, new TimeSpan(19, 0, 0));

        ImmutableDictionary<Guid, TimedItem> timed =
            ScheduleTimeline.Compute(Schedule(Block(Lane(start, dinner))), Friday);

        Assert.True(timed[dinner.Id].Clash);
        Assert.Equal(At(19, 0), timed[dinner.Id].Start);
        Assert.False(timed[start.Id].Clash);
    }

    [Fact]
    public void Adjustments_add_to_the_plan()
    {
        ScheduleItem start = Item("Start Night", 5, new TimeSpan(18, 30, 0), adjustment: 5);
        ScheduleItem songs = Item("Songs", 10);

        ImmutableDictionary<Guid, TimedItem> timed =
            ScheduleTimeline.Compute(Schedule(Block(Lane(start, songs))), Friday);

        Assert.Equal(At(18, 40), timed[songs.Id].Start);
    }

    [Fact]
    public void A_zero_minute_item_ending_a_split_lane_fills_to_the_end_of_the_split()
    {
        ScheduleItem start  = Item("Start Night", 10, new TimeSpan(18, 30, 0));
        ScheduleItem lesson = Item("Lesson", 25);
        ScheduleItem games  = Item("Games", 0);

        Schedule schedule = Schedule(Block(Lane(start)), Block(Lane(lesson), Lane(games)));

        ImmutableDictionary<Guid, TimedItem> timed = ScheduleTimeline.Compute(schedule, Friday);

        Assert.True(timed[games.Id].Fills);
        Assert.Equal(At(18, 40), timed[games.Id].Start);
        Assert.Equal(At(19, 5), timed[games.Id].End);
        Assert.Equal(0, ScheduleTimeline.Lanes(schedule.Blocks[1])[1].SpareMinutes);
    }

    [Fact]
    public void A_zero_minute_item_before_a_start_time_fills_up_to_it()
    {
        ScheduleItem start   = Item("Start Night", 10, new TimeSpan(18, 30, 0));
        ScheduleItem free    = Item("Free play", 0);
        ScheduleItem signOut = Item("Sign Out", 10, new TimeSpan(20, 30, 0));

        ImmutableDictionary<Guid, TimedItem> timed =
            ScheduleTimeline.Compute(Schedule(Block(Lane(start, free, signOut))), Friday);

        Assert.True(timed[free.Id].Fills);
        Assert.Equal(At(20, 30), timed[free.Id].End);
        Assert.False(timed[signOut.Id].Clash);
    }

    [Fact]
    public void The_total_is_first_start_to_last_finish_when_the_night_has_clock_times()
    {
        // 10 + 0 + 10 minutes of items, but Free play fills 18:40 to 20:30: the night is 18:30 to 20:40.
        Schedule schedule = Schedule(Block(Lane(
            Item("Start Night", 10, new TimeSpan(18, 30, 0)),
            Item("Free play", 0),
            Item("Sign Out", 10, new TimeSpan(20, 30, 0)))));

        Assert.Equal(130, ScheduleTimeline.TotalMinutes(schedule));
    }

    [Fact]
    public void The_night_is_underway_once_the_first_item_has_finished()
    {
        Schedule schedule = Schedule(Block(Lane(Item("Start Night", 5, new TimeSpan(18, 30, 0)), Item("Songs", 10))));

        Assert.False(ScheduleTimeline.IsUnderway(schedule, Friday, At(18, 34)));
        Assert.True(ScheduleTimeline.IsUnderway(schedule, Friday, At(18, 35)));
        Assert.False(ScheduleTimeline.IsUnderway(schedule, Friday, Friday.AddDays(-1).AddHours(19)));
    }

    [Fact]
    public void Without_a_clock_time_the_night_is_underway_from_its_day()
    {
        Schedule schedule = Schedule(Block(Lane(Item("Songs", 10))));

        Assert.False(ScheduleTimeline.IsUnderway(schedule, Friday, Friday.AddMinutes(-1)));
        Assert.True(ScheduleTimeline.IsUnderway(schedule, Friday, Friday.AddHours(9)));
    }
}
