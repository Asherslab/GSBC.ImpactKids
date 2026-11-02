namespace GSBC.ImpactKids.WASM.Features.DataDisplay.Components;

public partial class MetabaseEmbedDetails
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            EntityStore.RefreshAll()
        );
    }
}
