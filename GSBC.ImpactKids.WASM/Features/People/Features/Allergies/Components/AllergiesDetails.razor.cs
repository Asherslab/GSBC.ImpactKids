using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components;

public partial class AllergiesDetails : ComponentBase
{
    [Parameter]
    public ICollection<Allergy>? Allergies { get; set; }
    
    private ICollection<Allergy>? _allergies;
    private bool                  _waitingForUpdate;
    
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (Allergies != null)
        {
            _allergies = Allergies;
            _waitingForUpdate = false;
        }
        else
        {
            _waitingForUpdate = true;
        }
    }
}