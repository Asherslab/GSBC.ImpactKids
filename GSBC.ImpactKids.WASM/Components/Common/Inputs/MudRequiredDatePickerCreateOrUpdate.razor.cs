using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Common.Inputs;

public partial class MudRequiredDatePickerCreateOrUpdate
{
    [Parameter]
    public ModificationState State { get; set; } = ModificationState.Reading;

    [Parameter]
    public DateTime? Create { get; set; }

    [Parameter]
    public EventCallback<DateTime> CreateChanged { get; set; }

    [Parameter]
    public DateTime? Update { get; set; }

    [Parameter]
    public EventCallback<DateTime> UpdateChanged { get; set; }

    [Parameter]
    public DateTime? Read { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        Required = true;
        DateFormat = "dd/MM/yyyy";
        
        switch (State)
        {
            case ModificationState.Creating:
                Date = Create;
                DateChanged = EventCallback.Factory.Create<DateTime?>(
                    this,
                    OnDateChangedCreate
                );
                ReadOnly = false;
                break;
            case ModificationState.Reading:
                Date = Read;
                DateChanged = default;
                ReadOnly = true;
                break;
            case ModificationState.Updating:
                Date = Update;
                DateChanged = EventCallback.Factory.Create<DateTime?>(
                    this,
                    OnDateChangedUpdate
                );
                ReadOnly = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task OnDateChangedCreate(DateTime? date)
    {
        if (date != null)
        {
            Date = date.Value;
            await CreateChanged.InvokeAsync(date.Value);
        }
    }

    private async Task OnDateChangedUpdate(DateTime? date)
    {
        if (date != null)
        {
            Date = date.Value;
            await UpdateChanged.InvokeAsync(date.Value);
        }
    }
}