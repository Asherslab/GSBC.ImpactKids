using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Common.Inputs;

public partial class MudAutocompleteCreateOrUpdate<T>
{
    [Parameter]
    public ModificationState State { get; set; } = ModificationState.Reading;
    
    [Parameter]
    public required T Create { get; set; }
    
    [Parameter]
    public EventCallback<T> CreateChanged { get; set; }
    
    [Parameter]
    public required T Update { get; set; }
    
    [Parameter]
    public EventCallback<T> UpdateChanged { get; set; }
    
    [Parameter]
    public required T Read { get; set; }
    
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        
        switch (State)
        {
            case ModificationState.Creating:
                await SelectOptionAsync(Create);
                ValueChanged = CreateChanged;
                ReadOnly = false;
                break;
            case ModificationState.Reading:
                await SelectOptionAsync(Read);
                ValueChanged = new EventCallback<T>();
                ReadOnly = true;
                break;
            case ModificationState.Updating:
                await SelectOptionAsync(Update);
                ValueChanged = UpdateChanged;
                ReadOnly = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}