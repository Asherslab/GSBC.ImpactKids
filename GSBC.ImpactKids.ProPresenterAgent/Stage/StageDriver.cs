using GSBC.ImpactKids.ProPresenterAgent.ImpactKids;
using GSBC.ImpactKids.Shared.Contracts;
using GSBC.ImpactKids.ProPresenterAgent.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.ProPresenterAgent.Stage;

/// <summary>
/// Puts tonight's schedule on the stage: the one ProPresenter timer chosen on the admin page counts
/// down whatever is on now, and the now/next feeds give its label.
/// <para>
/// <b>Worked out here, from the schedule, with the same <see cref="ScheduleRun"/> rules the phones
/// use</b> - Impact Kids sends no commands, only "the schedule changed". A press of Next reaches this
/// Mac as that event (<see cref="EventStreamListener"/>), the agent re-reads tonight, and if what the
/// stage should show has changed it restarts the timer from the time left.
/// </para>
/// <para>
/// <b>The timer is only touched when something it shows changes</b> - the row, waiting or not, or
/// its zero time (a +5 moves that). Re-reading tonight every 30 seconds as a fallback therefore costs
/// nothing on the stage. A row picked up already in overtime starts from zero, so the overtime shown
/// is short by however long the agent was away; on a normal press it is exact.
/// </para>
/// </summary>
public sealed class StageDriver(
    ImpactKidsClient     impactKids,
    ProPresenterApi      proPresenter,
    AgentState           state,
    ILogger<StageDriver> logger
)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private volatile ProPresenterTonight? _tonight;
    private          string?              _applied;

    /// <summary>The row the timer was last started for, so a +5 on it moves the time left rather than starting it again.</summary>
    private string? _appliedRow;

    /// <summary>The row's zero time as the schedule had it, and as the stage's own clock has it, when the timer was last set.</summary>
    private DateTime _appliedTarget;
    private DateTime _stageEnd;

    private static DateTime ToUtc(DateTime churchTime) => ChurchTime.ToUtc(churchTime);

    /// <summary>Re-reads tonight from Impact Kids and puts it on the stage. Keeps the last known night if it cannot.</summary>
    public async Task RefreshAsync(CancellationToken token)
    {
        try
        {
            await impactKids.EnsureAuthenticatedAsync(token);

            BasicReadResponse<ProPresenterTonight> tonight =
                await impactKids.Agent.GetTonight(new ProPresenterAgentRequest(), token);

            if (tonight.Success)
                _tonight = tonight.Entity;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogWarning("Could not read tonight's schedule: {Message}", exception.Message);
        }

        await ApplyAsync(token);
    }

    private RunView? View(DateTime nowUtc) =>
        _tonight is { } tonight ? ScheduleRun.View(tonight.Schedule, tonight.ServiceDate, ToUtc, nowUtc) : null;

    /// <summary>The label for the now feed: the stage's row, or during a split the given group's own lane.</summary>
    public string NowLabel(string? group)
    {
        DateTime now = DateTime.UtcNow;

        if (View(now) is not { Now: not null } view || _tonight is not { } tonight)
            return "";

        return string.IsNullOrWhiteSpace(group)
            ? view.Now.Label
            : ScheduleRun.GroupLabel(tonight.Schedule, view, group, tonight.ServiceDate, ToUtc, now);
    }

    /// <summary>The label for the next feed: the next row in the schedule, by name. Before the night starts, the first.</summary>
    public string NextLabel() => View(DateTime.UtcNow)?.NextLabel ?? "";

    // Holding the timer for the labels - see ApplyAsync.

    /// <summary>A feed counts as read while ProPresenter fetched it within this long - two of its 10-second polls and a bit.</summary>
    private static readonly TimeSpan FeedReadWindow = TimeSpan.FromSeconds(25);

    /// <summary>The longest a timer waits for its labels to be fetched, if ProPresenter misses a poll. Its polls drift past 10 s.</summary>
    private static readonly TimeSpan LabelWaitLimit = TimeSpan.FromSeconds(15);

    /// <summary>What ProPresenter last fetched from one of the stage's feeds, and when.</summary>
    private sealed class FeedOnStage
    {
        public DateTime LastFetch = DateTime.MinValue;
        public string?  Label;

        public bool IsRead(DateTime now) => now - LastFetch < FeedReadWindow;

        /// <summary>True while ProPresenter reads this feed and has not yet been given <paramref name="label"/>.</summary>
        public bool Behind(DateTime now, string label) => IsRead(now) && Label != label;
    }

    private readonly FeedOnStage _nowFeed  = new();
    private readonly FeedOnStage _nextFeed = new();

    private bool    _holding;
    private string? _holdKey;

    /// <summary>
    /// Bumped by every new hold. A hold's fallback only ends that hold: an earlier fallback, still
    /// sleeping when a later press started a new hold, must not start the new row's timer early.
    /// </summary>
    private int _holdGeneration;

    /// <summary>ProPresenter has just fetched the stage's now feed and been given <paramref name="label"/>.</summary>
    public void NowFeedServed(string label) => FeedServed(_nowFeed, label);

    /// <summary>ProPresenter has just fetched the stage's next feed and been given <paramref name="label"/>.</summary>
    public void NextFeedServed(string label) => FeedServed(_nextFeed, label);

    /// <summary>
    /// A fetch lands its redraw <see cref="Feeds.LabelFeed.RedrawDelay"/> later. A held timer checks
    /// again then, and starts with that redraw if every label it was waiting for is now on the stage.
    /// </summary>
    private void FeedServed(FeedOnStage feed, string label)
    {
        feed.LastFetch = DateTime.UtcNow;
        feed.Label = label;

        if (!_holding)
            return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(Feeds.LabelFeed.RedrawDelay);
            await ApplyAsync(CancellationToken.None);
        });
    }

    /// <summary>
    /// Sets the chosen ProPresenter timer to match what is on now, if that has changed.
    /// <para>
    /// <b>A new row's timer waits for its labels.</b> ProPresenter only reads the label feeds every 10
    /// seconds, and nothing makes it read sooner - a stage redraw and a layout switch were both tried
    /// on 2026-10-20 and changed nothing. A timer started at once would count down under the previous
    /// row's labels for up to 10 seconds. So while any feed ProPresenter reads - now <b>and</b> next -
    /// has not yet been given its new text, the timer is held, and starts with the redraw of the
    /// fetch that brings the last of them. Both matter: Start Night going from waiting to running
    /// keeps its now label but its next label changes.
    /// </para>
    /// <para>
    /// Not held when every label is already right (a +5, or the next song within a section). A feed
    /// nothing reads is not waited for, and no hold lasts longer than <see cref="LabelWaitLimit"/>, so
    /// a missed poll cannot leave the timer on the old row.
    /// </para>
    /// </summary>
    public async Task ApplyAsync(CancellationToken token, int? fallbackFor = null)
    {
        if (state.Configuration?.ScheduleTimerUuid is not { } timerUuid)
            return;

        await _gate.WaitAsync(token);

        try
        {
            DateTime now  = DateTime.UtcNow;
            RunView? view = View(now);

            string key = view?.Now is { } row
                ? $"{timerUuid}|{row.Row.Id}|{row.Waiting}|{row.Target:O}"
                : $"{timerUuid}|idle";

            if (key == _applied)
                return;

            string label = view?.Now?.Label ?? "";
            string next  = view?.NextLabel ?? "";

            bool labelsBehind = _nowFeed.Behind(now, label) || _nextFeed.Behind(now, next);
            bool fallbackDue  = _holding && fallbackFor == _holdGeneration;

            if (labelsBehind && !fallbackDue)
            {
                // A new hold for a new state; a repeat check of the same state keeps the hold it has.
                if (!_holding || _holdKey != key)
                {
                    _holding = true;
                    _holdKey = key;
                    int generation = ++_holdGeneration;

                    logger.LogInformation("Stage: holding the timer for \"{Label}\" / next \"{Next}\" until ProPresenter shows them", label, next);

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(LabelWaitLimit);
                        await ApplyAsync(CancellationToken.None, fallbackFor: generation);
                    });
                }

                return;
            }

            if (fallbackDue && labelsBehind)
                logger.LogInformation("Stage: ProPresenter did not fetch the labels in time; starting the timer anyway");

            _holding = false;

            List<ProPresenterApi.TimerId>? timers = await proPresenter.GetTimersAsync(token);
            ProPresenterApi.TimerId?       timer  = timers?.FirstOrDefault(t => t.Uuid == timerUuid);

            if (timer == null)
            {
                logger.LogWarning("The schedule's timer is not in ProPresenter any more - choose another on the admin page");
                return;
            }

            if (view?.Now is { } onNow)
            {
                string rowKey = $"{timerUuid}|{onNow.Row.Id}|{onNow.Waiting}";

                // A row coming onto the stage starts from its full listed length - what is beside it
                // on the schedule - not the time left since Next was pressed, which the wait for
                // ProPresenter's label poll would have eaten into. A row with no length of its own
                // (fills until a time) and a row waiting for its start time count down to that time.
                // The same row changing (+5, −1) moves what is left, rather than starting it again.
                DateTime stageEnd;

                if (rowKey != _appliedRow)
                    stageEnd = onNow is { Waiting: false, Row.Minutes: > 0 }
                        ? now.AddMinutes(onNow.Row.Minutes)
                        : onNow.Target;
                else
                    // The stage's clock started later than the phone's (it waited for the label), so
                    // move its own finish by exactly the change rather than recount from the phone's.
                    stageEnd = _stageEnd + (onNow.Target - _appliedTarget);

                int seconds = (int)Math.Ceiling((stageEnd - now).TotalSeconds);

                // Waiting for a fixed start time: ProPresenter counts down to that time itself, which
                // shows hours as a time instead of as thousands of minutes.
                bool started = onNow is { Waiting: true, Row.StartsAt: { } startsAt }
                    ? await proPresenter.StartCountdownToTimeAsync(timer, startsAt, token)
                    : await proPresenter.StartCountdownAsync(timer, seconds, token);

                if (!started)
                    return;

                logger.LogInformation("Stage: {Label}{Waiting}, {Seconds}s on {Timer}",
                    onNow.Label, onNow.Waiting ? " (waiting for its start time)" : "", Math.Max(0, seconds), timer.Name);

                _appliedRow = rowKey;
                _appliedTarget = onNow.Target;
                _stageEnd = stageEnd;
            }
            else
            {
                await proPresenter.ResetAsync(timer, token);
                logger.LogInformation("Stage: nothing on - {Timer} reset", timer.Name);

                _appliedRow = null;
            }

            _applied = key;
        }
        finally
        {
            _gate.Release();
        }
    }
}
