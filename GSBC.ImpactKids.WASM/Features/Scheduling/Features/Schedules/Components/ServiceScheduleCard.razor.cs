using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;

/// <summary>A service's schedule at a glance on the service page, linking to the schedule itself.</summary>
public partial class ServiceScheduleCard
{
    [Parameter]
    public Guid? ServiceId { get; set; }

    private bool      _loaded;
    private Schedule? _schedule;
    private int       _items;
    private int       _unfilled;
    private DateTime? _start;
    private DateTime? _end;

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
            List<ScheduleItem> items = _schedule.Items.ToList();
            _items = items.Count;
            _unfilled = items.Count(i => i.NeedsFillIn && string.IsNullOrWhiteSpace(i.FillIn));

            // Only the clock times are shown, and those do not depend on which day is passed.
            ImmutableDictionary<Guid, TimedItem> timed = ScheduleTimeline.Compute(_schedule, DateTime.Today);
            _start = timed.Values.Where(t => t.Start != null).Select(t => t.Start).Min();
            _end = timed.Values.Where(t => t.End != null).Select(t => t.End).Max();
        }

        StateHasChanged();
    }
}
