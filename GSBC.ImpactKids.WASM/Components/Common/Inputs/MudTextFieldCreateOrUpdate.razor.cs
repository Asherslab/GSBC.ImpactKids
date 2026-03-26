using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Common.Inputs;

public partial class MudTextFieldCreateOrUpdate
{
    [Parameter]
    public ModificationState State { get; set; } = ModificationState.Reading;
    
    [Parameter]
    public string? Create { get; set; }
    
    [Parameter]
    public EventCallback<string?> CreateChanged { get; set; }
    
    [Parameter]
    public string? Update { get; set; }
    
    [Parameter]
    public EventCallback<string?> UpdateChanged { get; set; }
    
    [Parameter]
    public string? Read { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        
        switch (State)
        {
            case ModificationState.Creating:
                await SetTextAndUpdateValueAsync(Create);
                TextChanged = CreateChanged;
                ReadOnly = false;
                break;
            case ModificationState.Reading:
                await SetTextAndUpdateValueAsync(Read);
                TextChanged = default;
                ReadOnly = true;
                break;
            case ModificationState.Updating:
                await SetTextAndUpdateValueAsync(Update);
                TextChanged = UpdateChanged;
                ReadOnly = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}