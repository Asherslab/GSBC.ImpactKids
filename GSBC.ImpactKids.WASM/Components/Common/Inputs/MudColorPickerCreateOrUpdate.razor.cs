using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Common.Inputs;

public partial class MudColorPickerCreateOrUpdate
{
    [Parameter]
    public ModificationState State { get; set; }
    
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
                await SetTextAsync(Create, false);
                TextChanged = CreateChanged;
                ReadOnly = false;
                break;
            case ModificationState.Reading:
                await SetTextAsync(Read, false);
                TextChanged = default;
                ReadOnly = true;
                break;
            case ModificationState.Updating:
                await SetTextAsync(Update, false);
                TextChanged = UpdateChanged;
                ReadOnly = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}