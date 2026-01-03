namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.School.Components.Individual;

public partial class SchoolTermDetails
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            EntityStore.RefreshAll()
        );
    }
}