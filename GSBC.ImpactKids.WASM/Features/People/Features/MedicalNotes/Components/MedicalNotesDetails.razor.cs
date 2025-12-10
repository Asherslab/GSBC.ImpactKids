using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components;

public partial class MedicalNotesDetails : ComponentBase
{
    [Parameter]
    public ICollection<MedicalNote>? MedicalNotes { get; set; }
    
    private ICollection<MedicalNote>? _medicalNotes;
    private bool                      _waitingForUpdate;
    
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (MedicalNotes != null)
        {
            _medicalNotes = MedicalNotes;
            _waitingForUpdate = false;
        }
        else
        {
            _waitingForUpdate = true;
        }
    }
}