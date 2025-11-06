using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Multiple;

public partial class FamilyMembers : ComponentBase
{
    [Parameter]
    public ICollection<Person>? Members { get; set; }
}