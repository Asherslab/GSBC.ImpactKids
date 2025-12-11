using GSBC.ImpactKids.WASM.Features.Calendar.Models;
using GSBC.ImpactKids.WASM.Utilities;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Calendar.Components;

public partial class CalendarDay : ComponentBase
{
    [Parameter]
    public required DateTime Date { get; set; }

    /**
     * Whether this date card is for a previous or next month
     * in the calendar.
     * I.E. not part of the month the user is looking at currently.
     */
    [Parameter]
    public required bool OutOfMonth { get; set; }

    [Parameter]
    public ICollection<CalendarEvent>? Events { get; set; }

    [Parameter]
    public CalendarTerm? Term { get; set; }

    private string BackgroundColor => Term?.Color ?? "#006400";

    private string TermColor => Term == null
        ? ""
        : $"background: {BackgroundColor}";

    private string TermTextColorCss => $"color: {ContrastColor.GetAccessibleTextHex(BackgroundColor)}";
}