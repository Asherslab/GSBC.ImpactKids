using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;

namespace GSBC.ImpactKids.WASM.Components.Common.Inputs;

public partial class MudSelectCreateOrUpdate<T>
{
    [Parameter]
    public ModificationState State { get; set; } = ModificationState.Reading;
    
    [Parameter]
    public T? Create { get; set; }
    
    [Parameter]
    public EventCallback<T> CreateChanged { get; set; }
    
    [Parameter]
    public T? Update { get; set; }
    
    [Parameter]
    public EventCallback<T> UpdateChanged { get; set; }
    
    [Parameter]
    public T? Read { get; set; }
    
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        
        switch (State)
        {
            case ModificationState.Creating:
                SelectOption(Create);
                ValueChanged = CreateChanged;
                ReadOnly = false;
                break;
            case ModificationState.Reading:
                SelectOption(Read);
                ValueChanged = new EventCallback<T>();
                ReadOnly = true;
                break;
            case ModificationState.Updating:
                SelectOption(UpdateChanged);
                ValueChanged = UpdateChanged;
                ReadOnly = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}