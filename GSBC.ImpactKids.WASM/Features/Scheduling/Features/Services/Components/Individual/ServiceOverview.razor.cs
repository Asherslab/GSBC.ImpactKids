using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

public partial class ServiceOverview
{
    [Parameter]
    public required Service? Service { get; set; }

    [Parameter]
    public EventCallback<MouseEventArgs> DeleteService { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (Service == null)
        {
            if (_detailsState == ModificationState.Updating)
            {
                Snackbar.Add(
                    "Somebody else has made modifications to this service, your edit has been cancelled",
                    Severity.Warning,
                    x =>
                    {
                        x.CloseAfterNavigation = true;
                        x.VisibleStateDuration = int.MaxValue;
                    });
            }

            _detailsState = ModificationState.Reading;
        }
    }
    
    private ModificationState _detailsState = ModificationState.Reading;
    private ServiceDetails?   _serviceDetailsComponent;
    
    private async Task UpdateService()
    {
        if (_detailsState == ModificationState.Updating && _serviceDetailsComponent != null)
        {
            bool success = await _serviceDetailsComponent.UpdateService();
            if (success)
            {
                Service = null;
                _detailsState = ModificationState.Reading;
            }
        }
    }
}