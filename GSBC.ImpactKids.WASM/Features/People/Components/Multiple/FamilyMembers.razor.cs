using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Multiple;

public partial class FamilyMembers : ComponentBase
{
    [Parameter]
    public ICollection<Person>? Members { get; set; }

    // used to keep existing entities visible while they are updating in background
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