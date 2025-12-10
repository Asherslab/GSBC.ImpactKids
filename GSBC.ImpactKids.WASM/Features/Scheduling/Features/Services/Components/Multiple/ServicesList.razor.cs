using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Multiple;

public partial class ServicesList : ComponentBase
{
    [Parameter]
    public ICollection<Service>? Services { get; set; }
    
    // used to keep existing entities visible while they are updating in background
    private ICollection<Service>? _services;
    private bool                 _waitingForUpdate;
    
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (Services != null)
        {
            _services = Services;
            _waitingForUpdate = false;
        }
        else
        {
            _waitingForUpdate = true;
        }
    }
}