using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Components.Individual;

public partial class MemoryVerseDisplay : ComponentBase
{
    [Parameter]
    public MemoryVerse? MemoryVerse { get; set; }
    
    [Parameter]
    public string? Href { get; set; }
    
    [Parameter]
    public bool ShowReference { get; set; }
    
    [Parameter]
    public bool ShowVerseText { get; set; }
    
    [Parameter]
    public bool ShowOriginalText { get; set; }
    
    [Parameter]
    public bool ShowOriginalTextButton { get; set; }
    
    [Parameter]
    public int Elevation { set; get; } = 6;

    private string ReferenceOnlyClass => CssBuilder.Empty()
        .AddClass("clickable mud-ripple", Href != null)
        .AddClass("d-flex justify-start flex-direction-row flex-grow-1")
        .Build();
}