using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;

public partial class ScheduleTimeField
{
    [Parameter]
    public TimeSpan? Value { get; set; }

    [Parameter]
    public EventCallback<TimeSpan?> ValueChanged { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public string? HelperText { get; set; }

    [Parameter]
    public string? Class { get; set; }

    /// <summary>Every five minutes - plus the time already set, if it is not on one.</summary>
    private IEnumerable<int> Minutes =>
        Enumerable.Range(0, 12).Select(i => i * 5)
            .Append(Value?.Minutes ?? 0)
            .Distinct()
            .Order();

    // Picking an hour first gives :00, so one tap is enough for an on-the-hour time.
    private Task SetHour(int? hour) =>
        ValueChanged.InvokeAsync(hour == null ? null : new TimeSpan(hour.Value, Value?.Minutes ?? 0, 0));

    private Task SetMinute(int? minute) =>
        Value == null || minute == null
            ? Task.CompletedTask
            : ValueChanged.InvokeAsync(new TimeSpan(Value.Value.Hours, minute.Value, 0));
}
