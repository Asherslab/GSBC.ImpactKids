using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.DollarStore.Components.Individual;

public partial class DollarStoreEntryDetails
{
    [Parameter]
    public Guid? Id { get; set; }

    [Parameter]
    public Guid? ServiceId { get; set; }

    [Parameter]
    public ModificationState State { get; set; }

    private          AsyncData<DollarStoreEntry>   _dollarStoreEntry = AsyncData<DollarStoreEntry>.NotAsked();
    private readonly CreateDollarStoreEntryRequest _createRequest    = new();
    private          UpdateDollarStoreEntryRequest _updateRequest    = new();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

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
        _createRequest.ServiceId = ServiceId ?? Guid.Empty;
        if (State == ModificationState.Creating)
            return;

        AsyncData<ImmutableList<DollarStoreEntry>> entries = DollarStoreEntriesStore.GetState().Entities;

        if (!entries.HasData)
        {
            _dollarStoreEntry = _dollarStoreEntry.CopyStatus(entries);
            StateHasChanged();
            return;
        }

        DollarStoreEntry? entry = null;

        if (Id != null)
        {
            entry = entries.Data!
                .FirstOrDefault(x => x.Id == Id);
        }
        else if (ServiceId != null)
        {
            entry = entries.Data!
                .FirstOrDefault(x => x.ServiceId == ServiceId);
        }

        if (entry == null)
        {
            _dollarStoreEntry = _dollarStoreEntry.ToFailure("No Dollar Store Entry Found");
            _updateRequest = new UpdateDollarStoreEntryRequest();
            StateHasChanged();
            return;
        }

        _dollarStoreEntry = _dollarStoreEntry.ToSuccess(entry);

        _updateRequest = new UpdateDollarStoreEntryRequest
        {
            Guid = entry.Id
        };

        _updateRequest.DollarDoosMade.SetInitialValue(entry.DollarDoosMade);
        _updateRequest.Notes.SetInitialValue(entry.Notes);

        StateHasChanged();
    }

    public async Task<bool> CreateDollarStoreEntry()
    {
        _dollarStoreEntry = _dollarStoreEntry.ToLoading();
        StateHasChanged();
        BasicResponse resp = await DollarStoreEntryService.Create(_createRequest);

        if (resp.HasErrorOrNull())
        {
            RetrieveDollarStoreEntry();
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        return true;
    }

    public async Task<bool> UpdateDollarStoreEntry()
    {
        if (_updateRequest.Guid == Guid.Empty)
            return false;

        _dollarStoreEntry = _dollarStoreEntry.ToLoading();
        StateHasChanged();
        BasicResponse resp = await DollarStoreEntryService.Update(_updateRequest);

        if (!resp.HasErrorOrNull())
            return true;

        RetrieveDollarStoreEntry();
        Snackbar.AddErrorResponse(resp);
        return false;
    }

    public async Task DeleteDollarStoreEntry()
    {
        if (_dollarStoreEntry.Data == null)
            return;
        Guid id = _dollarStoreEntry.Data.Id;

        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;

        _dollarStoreEntry = _dollarStoreEntry.ToLoading();
        StateHasChanged();
        BasicReadRequest request = new() { Guid = id };
        BasicResponse    resp    = await DollarStoreEntryService.Delete(request);

        if (!resp.HasErrorOrNull())
            return;

        RetrieveDollarStoreEntry();
        Snackbar.AddErrorResponse(resp);
    }
}