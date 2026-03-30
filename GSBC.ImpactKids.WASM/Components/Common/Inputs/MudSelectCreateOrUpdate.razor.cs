using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Common.Inputs;

public partial class MudSelectCreateOrUpdate<T>
{
    [Parameter]
    public ModificationState State { get; set; } = ModificationState.Reading;
    
    [Parameter]
    public T? Create { get; set; }
    
    [Parameter]
    public EventCallback<T?> CreateChanged { get; set; }
    
    [Parameter]
    public T? Update { get; set; }
    
    [Parameter]
    public EventCallback<T?> UpdateChanged { get; set; }
    
    [Parameter]
    public T? Read { get; set; }
    
    [Parameter]
    public Func<T?, Task<bool>>? ErrorFunc { get; set; }
    
    protected override async Task OnParametersSetAsync()
    {
        base.OnParametersSet();
        
        switch (State)
        {
            case ModificationState.Creating:
                await SelectOption(Create);
                ValueChanged = CreateChanged;
                ReadOnly = false;
                break;
            case ModificationState.Reading:
                await SelectOption(Read);
                ValueChanged = new EventCallback<T?>();
                ReadOnly = true;
                break;
            case ModificationState.Updating:
                await SelectOption(Update);
                ValueChanged = UpdateChanged;
                ReadOnly = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        if (ErrorFunc != null)
            await ErrorState.SetValueAsync(await ErrorFunc(ReadValue));
    }
}