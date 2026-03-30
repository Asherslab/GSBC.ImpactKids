using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Common.Inputs;

public partial class MudDatePickerCreateOrUpdate
{
    [Parameter]
    public ModificationState State { get; set; } = ModificationState.Reading;
    
    [Parameter]
    public DateTime? Create { get; set; }
    
    [Parameter]
    public EventCallback<DateTime?> CreateChanged { get; set; }
    
    [Parameter]
    public DateTime? Update { get; set; }
    
    [Parameter]
    public EventCallback<DateTime?> UpdateChanged { get; set; }
    
    [Parameter]
    public DateTime? Read { get; set; }
    
    [Parameter]
    public Func<DateTime?, Task<bool>>? ErrorFunc { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        
        switch (State)
        {
            case ModificationState.Creating:
                await SetDateAsync(Create, true);
                DateChanged = CreateChanged;
                ReadOnly = false;
                break;
            case ModificationState.Reading:
                await SetDateAsync(Read, true);
                DateChanged = default;
                ReadOnly = true;
                break;
            case ModificationState.Updating:
                await SetDateAsync(Update, true);
                DateChanged = UpdateChanged;
                ReadOnly = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (ErrorFunc != null)
            await ErrorState.SetValueAsync(await ErrorFunc(Date));
    }
}