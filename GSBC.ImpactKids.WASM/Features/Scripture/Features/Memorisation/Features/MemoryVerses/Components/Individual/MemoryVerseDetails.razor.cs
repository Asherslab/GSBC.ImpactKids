namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerses.Components.Individual;

public partial class MemoryVerseDetails
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleStateChangeSubscriptionDisposal(MemoryVerseListsStore);

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            MemoryVerseListsStore.RefreshAll()
        );
    }
}