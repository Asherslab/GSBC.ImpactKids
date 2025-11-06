using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components;

public partial class AllergiesDetails : ComponentBase
{
    [Parameter]
    public ICollection<Allergy>? Allergies { get; set; }
}