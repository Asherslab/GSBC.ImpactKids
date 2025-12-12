using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Features.Calendar.Models;
using GSBC.ImpactKids.WASM.Utilities;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Calendar.Components;

public partial class CalendarEventDisplay
{
    [Parameter]
    public CalendarEvent? Event { get; set; }
    
    [Parameter]
    public string? Class { get; set; }

    private string BackgroundColor => Event?.Color ?? ServiceType.ColorBackup;
    private string TextColorCss    => $"color: {ContrastColor.GetAccessibleTextHex(BackgroundColor)}";
}