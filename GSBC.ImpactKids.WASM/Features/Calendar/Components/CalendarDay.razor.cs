using GSBC.ImpactKids.WASM.Features.Calendar.Models;
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
    
    private string TermStyle => Term == null
        ? ""
        : $"background: {Term.Color ?? "darkgreen"}";
    // "background: var(--mud-palette-drawer-icon); "
}