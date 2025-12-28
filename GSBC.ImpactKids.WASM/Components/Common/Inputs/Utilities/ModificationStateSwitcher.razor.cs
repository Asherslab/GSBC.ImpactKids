using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Common.Inputs.Utilities;

public partial class ModificationStateSwitcher : ComponentBase
{
    [Parameter]
    public ModificationState State { get; set; }

    [Parameter]
    public EventCallback<ModificationState> StateChanged { get; set; }

    [Parameter]
    public bool ShowCreate { get; set; }

    [Parameter]
    public EventCallback OnCreate { get; set; }

    [Parameter]
    public EventCallback OnUpdate { get; set; }

    [Parameter]
    public EventCallback OnDelete { get; set; }

    private async Task UpdateState(ModificationState state)
    {
        State = state;
        await StateChanged.InvokeAsync(State);
    }
}