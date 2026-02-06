namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerses.Pages;

public partial class Individual
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            EntityStore.RefreshAll()
        );
    }
}