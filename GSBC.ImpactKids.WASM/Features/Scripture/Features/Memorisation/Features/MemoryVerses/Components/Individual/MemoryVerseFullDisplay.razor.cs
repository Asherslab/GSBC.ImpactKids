using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerses.Components.Individual;

public partial class MemoryVerseFullDisplay
{
    [Parameter]
    public bool ShowReference { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            EntityStore.RefreshAll()
        );
    }
}