using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Features.ServiceTypes.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Components.Common.Inputs;

public partial class MudTextFieldCreateOrUpdate: MudTextField<string>
{
    [Parameter]
    public ModificationState State { get; set; } = ModificationState.Reading;
    
    [Parameter]
    public string? Create { get; set; }
    
    [Parameter]
    public EventCallback<string> CreateChanged { get; set; }
    
    [Parameter]
    public string? Update { get; set; }
    
    [Parameter]
    public EventCallback<string> UpdateChanged { get; set; }
    
    [Parameter]
    public string? Read { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        
        switch (State)
        {
            case ModificationState.Creating:
                Text = Create;
                TextChanged = CreateChanged;
                ReadOnly = false;
                break;
            case ModificationState.Reading:
                Text = Read;
                TextChanged = default;
                ReadOnly = true;
                break;
            case ModificationState.Updating:
                Text = Update;
                TextChanged = UpdateChanged;
                ReadOnly = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}