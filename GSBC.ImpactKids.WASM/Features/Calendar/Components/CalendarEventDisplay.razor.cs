using GSBC.ImpactKids.WASM.Features.Calendar.Models;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Calendar.Components;

public partial class CalendarEventDisplay
{
    [Parameter]
    public CalendarEvent? Event { get; set; }
}