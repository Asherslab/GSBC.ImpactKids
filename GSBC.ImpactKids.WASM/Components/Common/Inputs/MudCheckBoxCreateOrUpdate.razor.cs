using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Common.Inputs;

public partial class MudCheckBoxCreateOrUpdate
{
    [Parameter]
    public ModificationState State { get; set; } = ModificationState.Reading;
    
    [Parameter]
    public bool Create { get; set; }
    
    [Parameter]
    public EventCallback<bool> CreateChanged { get; set; }
    
    [Parameter]
    public bool Update { get; set; }
    
    [Parameter]
    public EventCallback<bool> UpdateChanged { get; set; }
    
    [Parameter]
    public bool Read { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        
        switch (State)
        {
            case ModificationState.Creating:
                Value = Create;
                ValueChanged = CreateChanged;
                ReadOnly = false;
                break;
            case ModificationState.Reading:
                Value = Read;
                ValueChanged = default;
                ReadOnly = true;
                break;
            case ModificationState.Updating:
                Value = Update;
                ValueChanged = UpdateChanged;
                ReadOnly = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}