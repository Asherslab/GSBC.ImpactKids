using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.DollarStore.Components.Individual;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

public partial class ServiceOverview
{
    [Parameter]
    public required Guid? Id { get; set; }

    private AsyncData<DollarStoreEntry> _dollarStoreEntry = AsyncData<DollarStoreEntry>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ServicesStore.Subscribe(_ =>
            {
                if (_detailsSent || _detailsState != ModificationState.Updating) return;

                Snackbar.Add(
                    "Somebody else has made modifications to this service, your edit has been cancelled",
                    Severity.Warning,
                    x =>
                    {
                        x.CloseAfterNavigation = true;
                        x.VisibleStateDuration = int.MaxValue;
                    });
                _detailsState = ModificationState.Reading;
                StateHasChanged();
            }
        );
        DollarStoreEntriesStore.Subscribe(_ => RetrieveDollarStoreEntry());

        await Task.WhenAll(DollarStoreEntriesStore.RefreshAll());
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        RetrieveDollarStoreEntry();
    }

    private void RetrieveDollarStoreEntry()
    {
        if (
            !_dollarStoreDetailsSent &&
            _dollarStoreState is ModificationState.Updating or ModificationState.Creating
        )
        {
            Snackbar.Add(
                "Somebody else has made modifications to the dollar store entry, your edit has been cancelled",
                Severity.Warning,
                x =>
                {
                    x.CloseAfterNavigation = true;
                    x.VisibleStateDuration = int.MaxValue;
                });
            _dollarStoreState = ModificationState.Reading;
        }

        AsyncData<ImmutableList<DollarStoreEntry>> entries = DollarStoreEntriesStore.GetState().Entities;

        if (!entries.HasData)
        {
            _dollarStoreEntry = _dollarStoreEntry.CopyStatus(entries);
            StateHasChanged();
            return;
        }

        DollarStoreEntry? entry = entries.Data!
            .FirstOrDefault(x => x.ServiceId == Id);

        if (entry == null)
        {
            _dollarStoreEntry = _dollarStoreEntry.ToFailure("No Dollar Store Entry Found");
            StateHasChanged();
            return;
        }

        _dollarStoreEntry = _dollarStoreEntry.ToSuccess(entry);
        StateHasChanged();
    }

    private bool              _detailsSent;
    private ModificationState _detailsState = ModificationState.Reading;
    private ServiceDetails?   _serviceDetailsComponent;

    private async Task UpdateService()
    {
        if (_detailsState == ModificationState.Updating && _serviceDetailsComponent != null)
        {
            _detailsSent = true;
            try
            {
                bool success = await _serviceDetailsComponent.UpdateEntity();
                if (success)
                    _detailsState = ModificationState.Reading;
            }
            finally
            {
                _detailsSent = false;
            }
        }
    }

    private async Task DeleteService()
    {
        if (_serviceDetailsComponent != null)
        {
            await _serviceDetailsComponent.DeleteEntity();
        }
    }

    private bool                     _dollarStoreDetailsSent;
    private ModificationState        _dollarStoreState = ModificationState.Reading;
    private DollarStoreEntryDetails? _dollarStoreDetailsComponent;

    private async Task CreateDollarStoreEntry()
    {
        if (_dollarStoreState == ModificationState.Creating && _dollarStoreDetailsComponent != null)
        {
            _dollarStoreDetailsSent = true;
            try
            {
                bool success = await _dollarStoreDetailsComponent.CreateEntity();
                if (success)
                    _dollarStoreState = ModificationState.Reading;
            }
            finally
            {
                _dollarStoreDetailsSent = false;
            }
        }
    }

    private async Task UpdateDollarStoreEntry()
    {
        if (_dollarStoreState == ModificationState.Updating && _dollarStoreDetailsComponent != null)
        {
            _dollarStoreDetailsSent = true;
            try
            {
                bool success = await _dollarStoreDetailsComponent.UpdateEntity();
                if (success)
                    _dollarStoreState = ModificationState.Reading;
            }
            finally
            {
                _dollarStoreDetailsSent = false;
            }
        }
    }

    private async Task DeleteDollarStoreEntry()
    {
        if (_dollarStoreDetailsComponent != null)
        {
            await _dollarStoreDetailsComponent.DeleteEntity();
        }
    }
}