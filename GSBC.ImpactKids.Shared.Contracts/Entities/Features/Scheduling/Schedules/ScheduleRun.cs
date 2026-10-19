using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

/// <summary>
/// One step of a night: a row of the schedule, or one item under a section row ("Way Maker" under
/// "Songs"). <see cref="Row"/> is what the stage shows; <see cref="Leaf"/> is what Next steps through.
/// </summary>
public sealed record RunStep(ScheduleItem Row, ScheduleItem Leaf, ScheduleBlock? Split, ScheduleLane? Lane);

/// <summary>The new start and end of one item after a press of Next. Both are written, whole.</summary>
public sealed record RunChange(Guid ItemId, DateTime? StartedAt, DateTime? EndedAt);

/// <summary>What one press of Next does: the items it starts and ends, and the row it leaves waiting, if any.</summary>
public sealed record RunPlan(ImmutableList<RunChange> Changes, Guid? WaitingItemId);

public enum RunState
{
    NotStarted,
    Running,
    Waiting,
    Finished
}

/// <summary>
/// What is on now for one track - the stage, or one lane of a split. <see cref="Target"/> is when its
/// countdown reaches zero (UTC); past it is overtime. <see cref="Leaf"/> is the song or game under a
/// section, for the phone; the stage only ever shows <see cref="Label"/>.
/// </summary>
public sealed record RunRow(ScheduleItem Row, ScheduleItem Leaf, string Label, DateTime Target, bool Waiting);

public sealed record RunView(RunState State, RunRow? Now, string? NextLabel, ScheduleBlock? Split);

/// <summary>
/// Running a schedule on the night - the rules agreed on 2026-10-19 (docs/work/2026-10-service-schedules.md):
/// <list type="bullet">
/// <item><b>Nothing moves by itself.</b> A leader presses Next; at zero the countdown goes into overtime
/// and stays there. Overtime is normal.</item>
/// <item>Next steps through the items under a section one at a time, recording each one's real
/// time, then on to the next row.</item>
/// <item><b>One main track drives the stage</b>: the main list, and during a split its lead lane (see
/// <see cref="LeadLane"/>). Every other lane of a split is a track of its own, with its own Next, so
/// games played in another room still get their times. Entering a split starts every lane; the lead
/// lane finishing ends the split, and whatever the other lanes were doing with it.</item>
/// <item><b>A fixed start time is waited for</b>, not jumped to: Next onto a row whose time is still
/// ahead leaves it waiting, counting down to its time, and the next Next starts it.</item>
/// </list>
/// Pure, so the server applies exactly what the phone predicts and the agent shows exactly what both see.
/// </summary>
public static class ScheduleRun
{
    /// <summary>The lane the stage follows during a split: the one chosen, else the one that sets the split's length.</summary>
    public static ScheduleLane? LeadLane(ScheduleBlock split) =>
        split.Lanes.FirstOrDefault(l => l.OnScreen)
        ?? split.Lanes.OrderByDescending(l => l.Items.Sum(i => i.Minutes)).FirstOrDefault();

    private static IEnumerable<RunStep> RowSteps(ScheduleItem row, ScheduleBlock? split, ScheduleLane? lane) =>
        row is { IsSection: true, Children.Count: > 0 }
            ? row.Children.Select(child => new RunStep(row, child, split, lane))
            : [new RunStep(row, row, split, lane)];

    /// <summary>The main track, in order: the main list's rows, and each split's lead lane in its place.</summary>
    public static ImmutableList<RunStep> MainSteps(Schedule schedule) =>
        ScheduleLayout.Entries(schedule)
            .SelectMany(entry => entry.Item != null
                ? RowSteps(entry.Item, null, null)
                : LeadLane(entry.Split!) is { } lead
                    ? lead.Items.SelectMany(row => RowSteps(row, entry.Split, lead))
                    : [])
            .ToImmutableList();

    public static ImmutableList<RunStep> LaneSteps(ScheduleBlock split, ScheduleLane lane) =>
        lane.Items.SelectMany(row => RowSteps(row, split, lane)).ToImmutableList();

    public static string Label(ScheduleItem row) => row.IsSection ? row.Name : row.DisplayName;

    /// <summary>
    /// What Next on the main track does at <paramref name="nowUtc"/>. Null when there is nothing to
    /// move on to. <paramref name="toUtc"/> turns the service's local wall-clock time into UTC.
    /// </summary>
    public static RunPlan? Next(Schedule schedule, DateTime serviceDate, Func<DateTime, DateTime> toUtc, DateTime nowUtc)
    {
        ImmutableList<RunStep> steps   = MainSteps(schedule);
        Changes                changes = new();

        int      currentIndex = steps.FindIndex(s => s.Leaf.IsRunning);
        RunStep? current      = currentIndex >= 0 ? steps[currentIndex] : null;
        int      nextIndex    = currentIndex >= 0 ? currentIndex + 1 : steps.FindIndex(s => s.Leaf.ActualStartedAt == null);
        RunStep? next         = nextIndex >= 0 && nextIndex < steps.Count ? steps[nextIndex] : null;

        if (current == null && next == null)
            return null;

        if (current != null)
        {
            changes.End(current.Leaf, nowUtc);

            if (current.Leaf != current.Row && next?.Row.Id != current.Row.Id)
                changes.End(current.Row, nowUtc);

            // Leaving a split ends it for every lane, whatever they were doing.
            if (current.Split != null && next?.Split?.Id != current.Split.Id)
                EndSplit(current.Split, current.Lane, changes, nowUtc);
        }

        if (next == null)
            return changes.Plan(null);

        // A fixed start time still ahead: wait for it rather than start early. The next press starts it.
        if (next.Row.StartsAt is { } startsAt
            && next.Row.ActualStartedAt == null
            && schedule.WaitingItemId != next.Row.Id
            && nowUtc < toUtc(serviceDate.Date + startsAt))
            return changes.Plan(next.Row.Id);

        Start(next, changes, nowUtc);

        // Entering a split starts every lane, not just the one on stage.
        if (next.Split != null && current?.Split?.Id != next.Split.Id)
        {
            foreach (ScheduleLane lane in next.Split.Lanes.Where(l => l.Id != next.Lane?.Id))
            {
                if (LaneSteps(next.Split, lane).FirstOrDefault() is { } first)
                    Start(first, changes, nowUtc);
            }
        }

        return changes.Plan(null);
    }

    /// <summary>What Next on one lane of a split does - a lane off the stage, stepping through its own items.</summary>
    public static RunPlan? NextInLane(Schedule schedule, Guid laneId, DateTime nowUtc)
    {
        foreach (ScheduleBlock split in schedule.Blocks.Where(b => b.IsSplit))
        {
            ScheduleLane? lane = split.Lanes.FirstOrDefault(l => l.Id == laneId);

            if (lane == null)
                continue;

            ImmutableList<RunStep> steps        = LaneSteps(split, lane);
            int                    currentIndex = steps.FindIndex(s => s.Leaf.IsRunning);

            // Not running: the split has not started, or this lane has finished and is waiting for the rest.
            if (currentIndex < 0)
                return null;

            Changes  changes = new();
            RunStep  current = steps[currentIndex];
            RunStep? next    = currentIndex + 1 < steps.Count ? steps[currentIndex + 1] : null;

            changes.End(current.Leaf, nowUtc);

            if (current.Leaf != current.Row && next?.Row.Id != current.Row.Id)
                changes.End(current.Row, nowUtc);

            if (next != null)
                Start(next, changes, nowUtc);

            return changes.Plan(schedule.WaitingItemId);
        }

        return null;
    }

    private static void Start(RunStep step, Changes changes, DateTime nowUtc)
    {
        if (step.Leaf != step.Row && step.Row.ActualStartedAt == null)
            changes.Start(step.Row, nowUtc);

        changes.Start(step.Leaf, nowUtc);
    }

    private static void EndSplit(ScheduleBlock split, ScheduleLane? lead, Changes changes, DateTime nowUtc)
    {
        foreach (ScheduleLane lane in split.Lanes.Where(l => l.Id != lead?.Id))
        {
            foreach (RunStep step in LaneSteps(split, lane).Where(s => s.Leaf.IsRunning))
            {
                changes.End(step.Leaf, nowUtc);
                if (step.Leaf != step.Row && step.Row.IsRunning)
                    changes.End(step.Row, nowUtc);
            }
        }
    }

    /// <summary>The state of the main track - the stage - at <paramref name="nowUtc"/>, and what comes next.</summary>
    public static RunView View(Schedule schedule, DateTime serviceDate, Func<DateTime, DateTime> toUtc, DateTime nowUtc)
    {
        ImmutableList<RunStep> steps = MainSteps(schedule);

        if (schedule.WaitingItemId is { } waitingId
            && steps.FirstOrDefault(s => s.Row.Id == waitingId) is { Row.StartsAt: { } startsAt } waiting)
        {
            // Not started yet, so it is still what comes next - as well as what the countdown is for.
            return new RunView(
                RunState.Waiting,
                new RunRow(waiting.Row, waiting.Leaf, Label(waiting.Row), toUtc(serviceDate.Date + startsAt), true),
                Label(waiting.Row),
                waiting.Split);
        }

        int index = steps.FindIndex(s => s.Leaf.IsRunning);

        if (index < 0)
        {
            RunState state = steps.Count > 0 && steps.All(s => s.Leaf.ActualEndedAt != null)
                ? RunState.Finished
                : RunState.NotStarted;

            string? first = state == RunState.NotStarted && steps.Count > 0 ? Label(steps[0].Row) : null;
            return new RunView(state, null, first, null);
        }

        RunStep step = steps[index];

        return new RunView(
            RunState.Running,
            Row(schedule, step, serviceDate, toUtc, nowUtc),
            NextRowLabel(steps, index),
            step.Split);
    }

    /// <summary>What one lane of a split is on now. Null when the lane is not running.</summary>
    public static RunRow? LaneNow(Schedule schedule, ScheduleBlock split, ScheduleLane lane, DateTime serviceDate,
                                  Func<DateTime, DateTime> toUtc, DateTime nowUtc) =>
        LaneSteps(split, lane).FirstOrDefault(s => s.Leaf.IsRunning) is { } step
            ? Row(schedule, step, serviceDate, toUtc, nowUtc)
            : null;

    /// <summary>
    /// What a group's own label shows: the stage's row, or during a split the row of the group's lane.
    /// Empty when the group's lane has finished early and waits for the rest.
    /// </summary>
    public static string GroupLabel(Schedule schedule, RunView view, string group, DateTime serviceDate,
                                    Func<DateTime, DateTime> toUtc, DateTime nowUtc)
    {
        if (view.Now == null)
            return "";

        if (view.Split == null || view.Now.Waiting)
            return view.Now.Label;

        ScheduleGroup? match = schedule.Groups.FirstOrDefault(g => string.Equals(g.Name, group, StringComparison.OrdinalIgnoreCase));
        ScheduleLane?  lane  = match == null ? null : view.Split.Lanes.FirstOrDefault(l => l.GroupIds.Contains(match.Id));

        if (lane == null || lane.Id == LeadLane(view.Split)?.Id)
            return view.Now.Label;

        return LaneNow(schedule, view.Split, lane, serviceDate, toUtc, nowUtc)?.Label ?? "";
    }

    private static RunRow Row(Schedule schedule, RunStep step, DateTime serviceDate, Func<DateTime, DateTime> toUtc, DateTime nowUtc) =>
        new(step.Row, step.Leaf, Label(step.Row), Target(schedule, step, serviceDate, toUtc, nowUtc), false);

    /// <summary>
    /// When the row's countdown reaches zero. Its start plus its length - or, for a 0-minute row that
    /// fills, the end of what it fills: a split's projected end for a lane off the stage, or the next
    /// fixed start time.
    /// </summary>
    private static DateTime Target(Schedule schedule, RunStep step, DateTime serviceDate, Func<DateTime, DateTime> toUtc, DateTime nowUtc)
    {
        DateTime started = step.Row.ActualStartedAt ?? nowUtc;

        if (step.Row.Minutes > 0)
            return started.AddMinutes(step.Row.Minutes);

        if (step.Split != null && step.Lane?.Id != LeadLane(step.Split)?.Id)
            return ProjectedSplitEnd(schedule, step.Split, nowUtc) ?? started;

        // A 0-minute row on the main track fills up to the next fixed start time, if one follows.
        ImmutableList<RunStep> steps = MainSteps(schedule);
        int                    index = steps.FindIndex(s => s.Row.Id == step.Row.Id);

        foreach (RunStep later in steps.Skip(index + 1))
        {
            if (later.Row.Id == step.Row.Id)
                continue;

            return later.Row.StartsAt is { } startsAt ? toUtc(serviceDate.Date + startsAt) : started;
        }

        return started;
    }

    /// <summary>When the lead lane will finish: its current row's countdown, then the rest of its rows' lengths.</summary>
    private static DateTime? ProjectedSplitEnd(Schedule schedule, ScheduleBlock split, DateTime nowUtc)
    {
        if (LeadLane(split) is not { } lead)
            return null;

        ScheduleItem? current = lead.Items.FirstOrDefault(i => i.IsRunning);

        if (current == null)
            return null;

        DateTime currentEnd = current.ActualStartedAt!.Value.AddMinutes(current.Minutes);
        if (currentEnd < nowUtc)
            currentEnd = nowUtc;

        int after = lead.Items.SkipWhile(i => i.Id != current.Id).Skip(1).Sum(i => i.Minutes);
        return currentEnd.AddMinutes(after);
    }

    private static string? NextRowLabel(ImmutableList<RunStep> steps, int index) =>
        steps.Skip(index + 1).FirstOrDefault(s => s.Row.Id != steps[index].Row.Id) is { } next ? Label(next.Row) : null;

    /// <summary>Collects the starts and ends one press makes, so each item is written once with both values.</summary>
    private sealed class Changes
    {
        private readonly Dictionary<Guid, (DateTime? Started, DateTime? Ended)> _changes = new();

        private (DateTime? Started, DateTime? Ended) Current(ScheduleItem item) =>
            _changes.TryGetValue(item.Id, out (DateTime?, DateTime?) changed)
                ? changed
                : (item.ActualStartedAt, item.ActualEndedAt);

        public void Start(ScheduleItem item, DateTime now) => _changes[item.Id] = (now, null);

        public void End(ScheduleItem item, DateTime now) => _changes[item.Id] = (Current(item).Started ?? now, now);

        public RunPlan Plan(Guid? waiting) =>
            new(_changes.Select(c => new RunChange(c.Key, c.Value.Started, c.Value.Ended)).ToImmutableList(), waiting);
    }
}
