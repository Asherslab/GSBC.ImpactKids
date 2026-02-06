using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Common;

public partial class DetailsModificationCard : ComponentBase
{
    [Parameter]
    public required Type DetailsComponentType { get; set; }

    [Parameter]
    public bool ShowCreate { get; set; }

    [Parameter]
    public Guid? Id { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public Dictionary<string, object?>? ExtraParameters { get; set; }

    private          ModificationState           _state;
    private          DynamicComponent?           _component;
    private readonly Dictionary<string, object?> _parameters = new();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        _parameters[nameof(IDetailsComponent.State)] = _state;
        _parameters[nameof(IDetailsComponent.Id)] = Id;
        _parameters[nameof(IDetailsComponent.OnStateChanged)] = new Action<ModificationState>(state => _state = state);
        if (ExtraParameters != null)
        {
            foreach (KeyValuePair<string, object?> keyValuePair in ExtraParameters)
            {
                _parameters[keyValuePair.Key] = keyValuePair.Value;
            }
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        _parameters[nameof(IDetailsComponent.Id)] = Id;
    }

    private void StateChanged(ModificationState state)
    {
        _state = state;
        _parameters[nameof(IDetailsComponent.State)] = _state;
    }

    private async Task OnCreate()
    {
        bool success = false;
        if (_component?.Instance is IDetailsComponent detailsComponent)
            success = await detailsComponent.CreateEntity();
        if (success)
            StateChanged(ModificationState.Reading);
    }

    private async Task OnUpdate()
    {
        bool success = false;
        if (_component?.Instance is IDetailsComponent detailsComponent)
            success = await detailsComponent.UpdateEntity();
        if (success)
            StateChanged(ModificationState.Reading);
    }

    private async Task OnDelete()
    {
        bool success = false;
        if (_component?.Instance is IDetailsComponent detailsComponent)
            success = await detailsComponent.DeleteEntity();
        if (success)
            StateChanged(ModificationState.Reading);
    }
}