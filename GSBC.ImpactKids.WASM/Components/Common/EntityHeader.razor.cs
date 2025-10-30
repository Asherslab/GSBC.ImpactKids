using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Common;

public partial class EntityHeader : ComponentBase
{
    [Parameter]
    public required string Name { get; set; }
    
    [Parameter]
    public required string? Subtitle { get; set; }
    
    [Parameter]
    public required EventCallback OnUpdate { get; set; }
    
    [Parameter]
    public required EventCallback OnDelete { get; set; }
}