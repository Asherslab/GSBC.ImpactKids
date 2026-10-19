using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

namespace GSBC.ImpactKids.Grpc.Tests.Scheduling;

/// <summary>
/// Pressing Next through a night. The server applies these plans, the phone predicts them and the
/// agent reads the resulting view for the stage, so these are the rules of the night itself.
/// </summary>
public class ScheduleRunTests
{
    private static readonly DateTime Friday = new(2026, 10, 23);

    // Tests run in "UTC = local" to keep the arithmetic readable.
    private static DateTime ToUtc(DateTime local) => local;

    private static DateTime At(int hour, int minute) => Friday.AddHours(hour).AddMinutes(minute);

    private static ScheduleItem Item(string name, int minutes, TimeSpan? startsAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        NeedsFillIn = false,
        PlannedMinutes = minutes,
        AdjustmentMinutes = 0,
        StartsAt = startsAt
    };

    private static ScheduleItem Section(string name, int minutes, params ScheduleItem[] children) =>
        Item(name, minutes) with { IsSection = true, Children = children.ToImmutableList() };

    private static ScheduleLane Lane(params ScheduleItem[] items) => new() { Id = Guid.NewGuid(), Items = items.ToImmutableList() };

    private static ScheduleBlock Block(params ScheduleLane[] lanes) => new() { Id = Guid.NewGuid(), Lanes = lanes.ToImmutableList() };

    private static Schedule Of(params ScheduleBlock[] blocks) => new() { Id = Guid.NewGuid(), Blocks = blocks.ToImmutableList() };

    /// <summary>Applies a plan the way the server does.</summary>
    private static Schedule Apply(Schedule schedule, RunPlan plan)
    {
        Dictionary<Guid, RunChange> changes = plan.Changes.ToDictionary(c => c.ItemId);

        ScheduleItem Update(ScheduleItem item) =>
            (changes.TryGetValue(item.Id, out RunChange? change)
                ? item with { ActualStartedAt = change.StartedAt, ActualEndedAt = change.EndedAt }
                : item) with { Children = item.Children.Select(Update).ToImmutableList() };

        return schedule with
        {
            WaitingItemId = plan.WaitingItemId,
            Blocks = schedule.Blocks.Select(b => b with
            {
                Lanes = b.Lanes.Select(l => l with { Items = l.Items.Select(Update).ToImmutableList() }).ToImmutableList()
            }).ToImmutableList()
        };
    }

    private static Schedule Next(Schedule schedule, DateTime now) =>
        Apply(schedule, ScheduleRun.Next(schedule, Friday, ToUtc, now)!);

    private static RunView View(Schedule schedule, DateTime now) => ScheduleRun.View(schedule, Friday, ToUtc, now);

    [Fact]
    public void Next_starts_the_night_then_steps_row_by_row()
    {
        Schedule night = Of(Block(Lane(Item("Intro", 5), Item("Lesson", 20))));

        night = Next(night, At(18, 30));
        Assert.Equal("Intro", View(night, At(18, 31)).Now!.Label);
        Assert.Equal("Lesson", View(night, At(18, 31)).NextLabel);
        Assert.Equal(At(18, 35), View(night, At(18, 31)).Now!.Target);

        night = Next(night, At(18, 37));
        RunView view = View(night, At(18, 38));
        Assert.Equal("Lesson", view.Now!.Label);
        Assert.Equal(At(18, 57), view.Now.Target);
        Assert.Equal(At(18, 37), night.Items.First().ActualEndedAt);
    }

    [Fact]
    public void Overtime_waits_for_Next()
    {
        Schedule night = Next(Of(Block(Lane(Item("Intro", 5), Item("Lesson", 20)))), At(18, 30));

        RunView late = View(night, At(18, 50));
        Assert.Equal("Intro", late.Now!.Label);
        Assert.True(late.Now.Target < At(18, 50));
    }

    [Fact]
    public void A_section_shows_its_own_name_while_Next_steps_through_its_songs()
    {
        ScheduleItem wayMaker = Item("Way Maker", 0), goodGrace = Item("Good Grace", 0);
        Schedule     night    = Of(Block(Lane(Section("Songs", 10, wayMaker, goodGrace), Item("Lesson", 20))));

        night = Next(night, At(18, 30));
        Assert.Equal("Songs", View(night, At(18, 31)).Now!.Label);
        Assert.Equal("Way Maker", View(night, At(18, 31)).Now!.Leaf.Name);
        Assert.Equal(At(18, 40), View(night, At(18, 31)).Now!.Target);
        Assert.Equal("Lesson", View(night, At(18, 31)).NextLabel);

        night = Next(night, At(18, 34));
        Assert.Equal("Good Grace", View(night, At(18, 35)).Now!.Leaf.Name);
        Assert.Equal(At(18, 40), View(night, At(18, 35)).Now!.Target);

        night = Next(night, At(18, 41));
        ScheduleItem songs = night.Items.First();
        Assert.Equal(At(18, 30), songs.ActualStartedAt);
        Assert.Equal(At(18, 41), songs.ActualEndedAt);
        Assert.Equal(At(18, 34), songs.Children[0].ActualEndedAt);
        Assert.Equal("Lesson", View(night, At(18, 42)).Now!.Label);
    }

    [Fact]
    public void A_fixed_start_time_ahead_is_waited_for_then_started_by_the_next_press()
    {
        Schedule night = Of(Block(Lane(Item("Snacks", 10), Item("Sign Out", 10, new TimeSpan(20, 30, 0)))));

        night = Next(night, At(20, 0));
        night = Next(night, At(20, 12));

        RunView waiting = View(night, At(20, 13));
        Assert.Equal(RunState.Waiting, waiting.State);
        Assert.Equal("Sign Out", waiting.Now!.Label);
        Assert.Equal(At(20, 30), waiting.Now.Target);
        Assert.Null(night.Items.Last().ActualStartedAt);

        night = Next(night, At(20, 31));
        Assert.Equal(RunState.Running, View(night, At(20, 32)).State);
        Assert.Equal(At(20, 31), night.Items.Last().ActualStartedAt);
    }

    [Fact]
    public void A_split_follows_the_lead_lane_on_stage_and_starts_and_ends_every_lane()
    {
        ScheduleItem lesson = Item("Lesson", 25), games = Item("Games", 0);
        ScheduleLane lessons = Lane(lesson), gamesLane = Lane(games);
        Schedule     night   = Of(Block(Lane(Item("Intro", 5))), Block(lessons, gamesLane), Block(Lane(Item("Snacks", 10))));

        night = Next(night, At(18, 30));
        night = Next(night, At(18, 35));

        RunView view = View(night, At(18, 36));
        Assert.Equal("Lesson", view.Now!.Label);
        Assert.NotNull(night.Blocks[1].Lanes[1].Items[0].ActualStartedAt);

        // Games fills: its countdown is the lesson's end.
        RunRow? gamesNow = ScheduleRun.LaneNow(night, night.Blocks[1], night.Blocks[1].Lanes[1], Friday, ToUtc, At(18, 36));
        Assert.Equal(At(19, 0), gamesNow!.Target);

        night = Next(night, At(19, 2));
        Assert.Equal("Snacks", View(night, At(19, 3)).Now!.Label);
        Assert.Equal(At(19, 2), night.Blocks[1].Lanes[1].Items[0].ActualEndedAt);
    }

    [Fact]
    public void A_lane_off_stage_steps_through_its_own_games()
    {
        ScheduleItem dodgeball = Item("Dodgeball", 0), tag = Item("Tag", 0);
        ScheduleBlock split = Block(Lane(Item("Lesson", 25)), Lane(Section("Games", 0, dodgeball, tag)));
        Schedule night = Next(Of(split), At(18, 30));

        Guid gamesLane = night.Blocks[0].Lanes[1].Id;
        night = Apply(night, ScheduleRun.NextInLane(night, gamesLane, At(18, 41))!);

        ScheduleItem games = night.Blocks[0].Lanes[1].Items[0];
        Assert.Equal(At(18, 41), games.Children[0].ActualEndedAt);
        Assert.Equal(At(18, 41), games.Children[1].ActualStartedAt);
        Assert.True(games.IsRunning);
    }

    [Fact]
    public void A_group_label_follows_its_own_lane_during_a_split()
    {
        Schedule night = Of(Block(Lane(Item("Lesson", 25)), Lane(Item("Games", 0))));
        Guid     prep  = Guid.NewGuid(), older = Guid.NewGuid();

        night = night with
        {
            Groups = [new ScheduleGroup { Id = prep, Name = "Prep-3" }, new ScheduleGroup { Id = older, Name = "4-6" }],
            Blocks =
            [
                night.Blocks[0] with
                {
                    Lanes =
                    [
                        night.Blocks[0].Lanes[0] with { GroupIds = [prep] },
                        night.Blocks[0].Lanes[1] with { GroupIds = [older] }
                    ]
                }
            ]
        };

        night = Next(night, At(18, 30));
        RunView view = View(night, At(18, 31));

        Assert.Equal("Lesson", ScheduleRun.GroupLabel(night, view, "prep-3", Friday, ToUtc, At(18, 31)));
        Assert.Equal("Games", ScheduleRun.GroupLabel(night, view, "4-6", Friday, ToUtc, At(18, 31)));
    }

    [Fact]
    public void A_chosen_lane_is_followed_on_stage_over_the_longer_one()
    {
        ScheduleBlock split = Block(Lane(Item("Lesson", 25)), Lane(Item("Games", 0)) with { OnScreen = true });

        Assert.Equal("Games", ScheduleRun.LeadLane(split)!.Items[0].Name);
    }
}
