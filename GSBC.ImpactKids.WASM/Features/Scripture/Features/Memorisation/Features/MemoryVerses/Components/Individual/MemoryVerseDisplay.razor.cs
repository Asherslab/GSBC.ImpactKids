using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerses.Components.Individual;

public partial class MemoryVerseDisplay
{
    [Parameter]
    public string? Href { get; set; }

    private string Class => CssBuilder.Empty()
        .AddClass("clickable mud-ripple", Href != null)
        .AddClass("d-flex justify-start flex-direction-row flex-grow-1")
        .Build();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            EntityStore.RefreshAll()
        );
    }
}