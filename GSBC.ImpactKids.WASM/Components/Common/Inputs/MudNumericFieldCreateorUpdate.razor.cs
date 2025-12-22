using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Common.Inputs;

public partial class MudNumericFieldCreateorUpdate<T>
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

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        
        switch (State)
        {
            case ModificationState.Creating:
                await SetValueAsync(Create);
                ValueChanged = CreateChanged;
                ReadOnly = false;
                break;
            case ModificationState.Reading:
                await SetValueAsync(Read);
                ValueChanged = default;
                ReadOnly = true;
                break;
            case ModificationState.Updating:
                await SetValueAsync(Update);
                ValueChanged = UpdateChanged;
                ReadOnly = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}