using GSBC.ImpactKids.Shared.Contracts;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;

/// <summary>
/// Next and Back on the night. The press goes to the server, which applies it under the schedule's
/// lock and tells every phone and the ProPresenter agent; this bar then shows the result. Buttons are
/// disabled while a press is in flight, so one leader's double tap is one press - two leaders pressing
/// at once still count twice, in order.
/// </summary>
public partial class ScheduleRunBar
{
    [Parameter]
    public required Schedule? Schedule { get; set; }

    /// <summary>The service's local date - the day its fixed start times are on.</summary>
    [Parameter]
    public required DateTime LocalDate { get; set; }

    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    private RunView _view = new(RunState.NotStarted, null, null, null);
    private bool    _busy;
    private Timer?  _tick;

    // The church's clock, as the server and the ProPresenter agent read fixed start times - not the
    // phone's own zone, or a phone set to Sydney in summer and the stage would disagree about 18:30.
    private static DateTime ToUtc(DateTime churchTime) => ChurchTime.ToUtc(churchTime);

    protected override void OnInitialized()
    {
        _tick = new Timer(_ => InvokeAsync(() =>
        {
            Recompute();
            StateHasChanged();
        }), null, Tick, Tick);
    }

    protected override void OnParametersSet() => Recompute();

    private void Recompute()
    {
        if (Schedule != null)
            _view = ScheduleRun.View(Schedule, LocalDate.Date, ToUtc, DateTime.UtcNow);
    }

    private static string Clock(TimeSpan left)
    {
        TimeSpan abs = left.Duration();
        string   text = abs.TotalHours >= 1 ? $"{(int)abs.TotalHours}:{abs.Minutes:00}:{abs.Seconds:00}" : $"{abs.Minutes}:{abs.Seconds:00}";
        return left < TimeSpan.Zero ? $"+{text}" : text;
    }

    private string LaneName(ScheduleLane lane)
    {
        List<string> names = Schedule!.Groups.Where(g => lane.GroupIds.Contains(g.Id)).Select(g => g.Name).ToList();
        return names.Count == 0 ? "Other lane" : string.Join(", ", names);
    }

    private Task Next(Guid? laneId) => Press(() => ScheduleService.RunNext(new RunScheduleRequest { ScheduleId = Schedule!.Id, LaneId = laneId }));

    private Task Back(Guid? laneId) => Press(() => ScheduleService.RunBack(new RunScheduleRequest { ScheduleId = Schedule!.Id, LaneId = laneId }));

    private async Task Press(Func<Task<BasicResponse>> call)
    {
        if (Schedule == null || _busy)
            return;

        _busy = true;

        try
        {
            Snackbar.AddErrorResponse(await call());
            await SchedulesStore.RefreshEvent();
        }
        finally
        {
            _busy = false;
        }
    }

    public override void Dispose()
    {
        _tick?.Dispose();
        base.Dispose();
    }
}
