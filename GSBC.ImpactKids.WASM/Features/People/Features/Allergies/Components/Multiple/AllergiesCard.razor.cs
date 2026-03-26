using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components.Individual;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components.Multiple;

public partial class AllergiesCard : ComponentBase
{
    [Parameter]
    public Guid? PersonId { get; set; }

    [Parameter]
    public EventCallback<bool> NoIdsChanged { get; set; }
    
    private async Task CreateAllergy() =>
        await DetailsComponentDialog.Open<AllergyDetails>(
            DialogService,
            "Create Allergy",
            ModificationState.Creating,
            extraParameters: new Dictionary<string, object?> { { nameof(AllergyDetails.PersonId), PersonId } }
        );
}