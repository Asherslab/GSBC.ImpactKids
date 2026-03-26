namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonContactDetails
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            EntityStore.RefreshAll()
        );
    }
}