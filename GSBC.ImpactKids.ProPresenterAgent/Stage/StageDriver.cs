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

    /// <summary>Sets the chosen ProPresenter timer to match what is on now, if that has changed.</summary>
    public async Task ApplyAsync(CancellationToken token)
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
