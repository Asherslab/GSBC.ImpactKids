using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

public partial class ServiceDisplay : ComponentBase
{
    [Parameter]
    public required Service? Service { get; set; }
}