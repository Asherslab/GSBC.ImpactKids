using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerseLists.Components.Individual;

public partial class MemoryVerseListDetails
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleStateChangeSubscriptionDisposal(SchoolTermsStore);

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            SchoolTermsStore.RefreshAll()
        );
    }

    private ICollection<SchoolTerm> GetSchoolTermsForDropdown()
    {
        ImmutableList<SchoolTerm>? schoolTerms = SchoolTermsStore.GetState().Entities.Data;
        if (schoolTerms == null)
            return [];
        return schoolTerms;
    }
}