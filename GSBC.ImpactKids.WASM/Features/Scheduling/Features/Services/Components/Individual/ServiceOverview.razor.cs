using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Features.DollarStore.Components.Individual;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

public partial class ServiceOverview
{
    [Parameter]
    public required Service? Service { get; set; }

    [Parameter]
    public required ICollection<MemoryVerse>? MemoryVerses { get; set; }

    [Parameter]
    public EventCallback<MouseEventArgs> DeleteService { get; set; }

    [Parameter]
    public EventCallback<MouseEventArgs> DeleteDollarStore { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (Service == null)
        {
            if (
                _detailsState == ModificationState.Updating ||
                _dollarStoreState == ModificationState.Updating
            )
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
            _dollarStoreState = ModificationState.Reading;
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

    private ModificationState        _dollarStoreState = ModificationState.Reading;
    private DollarStoreEntryDetails? _dollarStoreDetailsComponent;

    private async Task CreateDollarStoreEntry()
    {
        if (_dollarStoreState == ModificationState.Creating && _dollarStoreDetailsComponent != null)
        {
            bool success = await _dollarStoreDetailsComponent.CreateDollarStoreEntry();
            if (success)
            {
                Service?.DollarStoreEntry = null;
                _dollarStoreState = ModificationState.Reading;
            }
        }
    }
    
    private async Task UpdateDollarStoreEntry()
    {
        if (_dollarStoreState == ModificationState.Updating && _dollarStoreDetailsComponent != null)
        {
            bool success = await _dollarStoreDetailsComponent.UpdateDollarStoreEntry();
            if (success)
            {
                Service?.DollarStoreEntry = null;
                _dollarStoreState = ModificationState.Reading;
            }
        }
    }
}