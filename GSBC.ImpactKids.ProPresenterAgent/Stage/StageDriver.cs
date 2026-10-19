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

    // Holding the timer for the label - see ApplyAsync.

    /// <summary>A stage now feed counts as read while ProPresenter fetched it within this long - two of its 10-second polls and a bit.</summary>
    private static readonly TimeSpan FeedReadWindow = TimeSpan.FromSeconds(25);

    /// <summary>The longest a new row's timer waits for its label to be fetched, if ProPresenter misses a poll.</summary>
    private static readonly TimeSpan LabelWaitLimit = TimeSpan.FromSeconds(12);

    private DateTime _lastNowFetch = DateTime.MinValue;
    private string?  _labelOnStage;
    private string?  _waitingFor;

    /// <summary>
    /// ProPresenter has just fetched the stage's now feed and been given <paramref name="label"/>; its
    /// redraw lands <see cref="Feeds.LabelFeed.RedrawDelay"/> later. A timer held back for this label
    /// starts at that same moment.
    /// </summary>
    public void NowFeedServed(string label)
    {
        _lastNowFetch = DateTime.UtcNow;
        _labelOnStage = label;

        if (_waitingFor == label)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(Feeds.LabelFeed.RedrawDelay);
                await ApplyAsync(CancellationToken.None);
            });
        }
    }

    /// <summary>
    /// Sets the chosen ProPresenter timer to match what is on now, if that has changed.
    /// <para>
    /// <b>A new row's timer waits for its label.</b> ProPresenter only reads the label feed every 10
    /// seconds, and nothing makes it read sooner - a stage redraw and a layout switch were both tried
    /// on 2026-10-20 and changed nothing. A timer started at once would count down under the previous
    /// row's name for up to 10 seconds. So when the label changes the timer is held until ProPresenter
    /// has fetched the new label (<see cref="NowFeedServed"/>), and starts with the redraw. The seconds
    /// are worked out then, so the wait is already taken off.
    /// </para>
    /// <para>
    /// Not held when the label is unchanged (a +5, or the next song within a section) - the screen is
    /// already right. Not held when nothing reads the now feed, and never for longer than
    /// <see cref="LabelWaitLimit"/>, so a missed poll cannot leave the timer on the old row.
    /// </para>
    /// </summary>
    public async Task ApplyAsync(CancellationToken token, bool waitLimitReached = false)
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

            bool feedIsRead    = now - _lastNowFetch < FeedReadWindow;
            bool labelOnScreen = label == _labelOnStage;

            if (feedIsRead && !labelOnScreen && !waitLimitReached)
            {
                if (_waitingFor != label)
                {
                    _waitingFor = label;
                    logger.LogInformation("Stage: holding the timer for \"{Label}\" until ProPresenter shows it", label);

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(LabelWaitLimit);
                        await ApplyAsync(CancellationToken.None, waitLimitReached: true);
                    });
                }

                return;
            }

            _waitingFor = null;

            List<ProPresenterApi.TimerId>? timers = await proPresenter.GetTimersAsync(token);
            ProPresenterApi.TimerId?       timer  = timers?.FirstOrDefault(t => t.Uuid == timerUuid);

            if (timer == null)
            {
                logger.LogWarning("The schedule's timer is not in ProPresenter any more - choose another on the admin page");
                return;
            }

            if (view?.Now is { } onNow)
            {
                int seconds = (int)Math.Ceiling((onNow.Target - now).TotalSeconds);

                if (!await proPresenter.StartCountdownAsync(timer, seconds, token))
                    return;

                logger.LogInformation("Stage: {Label}{Waiting}, {Seconds}s on {Timer}",
                    onNow.Label, onNow.Waiting ? " (waiting for its start time)" : "", Math.Max(0, seconds), timer.Name);
            }
            else
            {
                await proPresenter.ResetAsync(timer, token);
                logger.LogInformation("Stage: nothing on - {Timer} reset", timer.Name);
            }

            _applied = key;
        }
        finally
        {
            _gate.Release();
        }
    }
}
