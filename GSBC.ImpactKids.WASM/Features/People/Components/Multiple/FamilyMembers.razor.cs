using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Multiple;

public partial class FamilyMembers : ComponentBase
{
    [Parameter]
    public ICollection<Person>? Members { get; set; }

    private ICollection<Person>? _familyMembers;
    private bool                 _waitingForUpdate;
    
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (Members != null)
        {
            _familyMembers = Members;
            _waitingForUpdate = false;
        }
        else
        {
            _waitingForUpdate = true;
        }
    }
}