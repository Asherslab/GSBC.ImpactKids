using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components.Individual;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components.Multiple;

public partial class MedicalNotesCard : ComponentBase
{
    [Parameter]
    public Guid? PersonId { get; set; }

    [Parameter]
    public EventCallback<bool> NoIdsChanged { get; set; }
    
    private async Task CreateMedicalNote() =>
        await DetailsComponentDialog.Open<MedicalNoteDetails>(
            DialogService,
            "Create Medical Note",
            ModificationState.Creating,
            extraParameters: new Dictionary<string, object?> { { nameof(MedicalNoteDetails.PersonId), PersonId } }
        );
}