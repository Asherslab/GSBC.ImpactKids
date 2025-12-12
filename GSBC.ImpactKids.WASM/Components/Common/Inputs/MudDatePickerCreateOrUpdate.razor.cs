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

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        
        DateFormat = "dd/MM/yyyy";
        
        switch (State)
        {
            case ModificationState.Creating:
                Date = Create;
                DateChanged = CreateChanged;
                ReadOnly = false;
                break;
            case ModificationState.Reading:
                Date = Read;
                DateChanged = default;
                ReadOnly = true;
                break;
            case ModificationState.Updating:
                Date = Update;
                DateChanged = UpdateChanged;
                ReadOnly = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}