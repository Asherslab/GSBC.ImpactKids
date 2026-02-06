using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components.Individual;
using GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components.Individual;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonOverview
{
    [Parameter]
    public required Guid? Id { get; set; }

    private async Task CreateMedicalNote() =>
        await DetailsComponentDialog.Open<MedicalNoteDetails>(
            DialogService,
            "Create Medical Note",
            ModificationState.Creating,
            extraParameters: new Dictionary<string, object?> { { nameof(MedicalNoteDetails.PersonId), Id } }
        );

    private async Task CreateAllergy() =>
        await DetailsComponentDialog.Open<AllergyDetails>(
            DialogService,
            "Create Allergy",
            ModificationState.Creating,
            extraParameters: new Dictionary<string, object?> { { nameof(AllergyDetails.PersonId), Id } }
        );
}