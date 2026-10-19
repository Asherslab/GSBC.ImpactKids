using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;

/// <summary>
/// The night at a glance on the service page: line by line with start times, read only. A split of
/// up to <see cref="MaxLanesShown"/> lanes shows its lanes side by side; a wider one would not fit a
/// card, so it becomes one "Split" line with its times.
/// </summary>
public partial class ServiceScheduleCard
{
    private const int MaxLanesShown = 2;

    [Parameter]
    public Guid? ServiceId { get; set; }

    private bool                                 _loaded;
    private Schedule?                            _schedule;
    private ImmutableList<ScheduleEntry>         _entries = [];
    private ImmutableDictionary<Guid, TimedItem> _timed   = ImmutableDictionary<Guid, TimedItem>.Empty;
    private int                                  _unfilled;
    private DateTime?                            _start;
    private DateTime?                            _end;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(SchedulesStore, Retrieve);
        Retrieve();

        await SchedulesStore.RefreshAll();
    }

    protected override void OnParametersSet() => Retrieve();

    private void Retrieve()
    {
        ImmutableList<Schedule>? schedules = SchedulesStore.GetState().Entities.Data;

        _loaded = schedules != null;
        _schedule = schedules?.FirstOrDefault(x => x.ServiceId == ServiceId && ServiceId != null);

        if (_schedule != null)
        {
            _entries = ScheduleLayout.Entries(_schedule);
            _unfilled = _schedule.AllItems.Count(i => i.NeedsFillIn && string.IsNullOrWhiteSpace(i.FillIn));

            // Only the clock times are shown, and those do not depend on which day is passed.
            _timed = ScheduleTimeline.Compute(_schedule, DateTime.Today);
            _start = _timed.Values.Where(t => t.Start != null).Select(t => t.Start).Min();
            _end = _timed.Values.Where(t => t.End != null).Select(t => t.End).Max();
        }

        StateHasChanged();
    }

    private static string Clock(DateTime? time) => time is { } t ? ScheduleFormat.Clock(t) : "";

    private DateTime? SplitStart(ScheduleBlock split) =>
        split.Lanes.SelectMany(l => l.Items).Select(i => _timed.GetValueOrDefault(i.Id)?.Start).Where(s => s != null).Min();

    private string SplitLength(ScheduleBlock split)
    {
        ImmutableList<LaneTiming> lanes = ScheduleTimeline.Lanes(split);
        return ScheduleFormat.Minutes(lanes.Count == 0 ? 0 : lanes.Max(l => l.Minutes));
    }

    private string LaneName(ScheduleLane lane)
    {
        List<string> names = _schedule!.Groups.Where(g => lane.GroupIds.Contains(g.Id)).Select(g => g.Name).ToList();
        return names.Count == 0 ? "No group" : string.Join(", ", names);
    }
}
