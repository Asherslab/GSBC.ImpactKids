using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Components.Individual;

public partial class MemoryVerseDisplay
{
    [Parameter]
    public string? Href { get; set; }

    [Parameter]
    public bool ShowReference { get; set; }

    private string ReferenceOnlyClass => CssBuilder.Empty()
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