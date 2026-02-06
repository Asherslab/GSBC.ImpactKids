namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Components.Individual;

public partial class BibleVerseDetails
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            EntityStore.RefreshAll()
        );
    }
}