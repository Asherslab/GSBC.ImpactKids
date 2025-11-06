using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components;

public partial class MedicalNotesDetails : ComponentBase
{
    [Parameter]
    public ICollection<MedicalNote>? MedicalNotes { get; set; }
}