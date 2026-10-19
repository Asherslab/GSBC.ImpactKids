using System.Globalization;
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

    // <input type="time"> speaks "HH:mm" whatever the phone's locale shows.
    private string? Text => Value is { } value ? $"{value.Hours:00}:{value.Minutes:00}" : null;

    private Task OnChanged(string? text)
    {
        TimeSpan? value = TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan parsed)
            ? parsed
            : null;

        return ValueChanged.InvokeAsync(value);
    }
}
