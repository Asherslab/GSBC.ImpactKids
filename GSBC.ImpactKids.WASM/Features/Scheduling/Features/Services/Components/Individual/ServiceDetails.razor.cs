using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

public partial class ServiceDetails
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        ServiceTypesStore.Subscribe(_ => StateHasChanged());
        SchoolTermsStore.Subscribe(_ => StateHasChanged());

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            ServiceTypesStore.RefreshAll(),
            SchoolTermsStore.RefreshAll()
        );
    }

    private ICollection<SchoolTerm> GetSchoolTermsForDropdown()
    {
        ImmutableList<SchoolTerm>? schoolTerms = SchoolTermsStore.GetState().Entities.Data;
        if (schoolTerms == null)
            return [];

        switch (State)
        {
            case ModificationState.Creating:
            {
                int year = CreateRequest.LocalDate.Year;

                return schoolTerms
                    .Where(x => x.LocalStartDate.Year == year)
                    .ToList();
            }
            case ModificationState.Updating:
            {
                int? year = UpdateRequest.LocalDate.Value.Year;

                return schoolTerms
                    .Where(x => x.LocalStartDate.Year == year)
                    .ToList();
            }
            case ModificationState.Reading:
            default:
                return schoolTerms;
        }
    }
}