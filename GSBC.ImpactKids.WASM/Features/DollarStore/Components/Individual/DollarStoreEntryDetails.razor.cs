using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.DollarStore.Components.Individual;

public partial class DollarStoreEntryDetails
{
    [Parameter]
    public DollarStoreEntry? DollarStoreEntry { get; set; }

    [Parameter]
    public Guid? ServiceId { get; set; }

    [Parameter]
    public ModificationState State { get; set; }

    [Parameter]
    public bool DisableOverlay { get; set; }

    private readonly CreateDollarStoreEntryRequest _createRequest = new();
    private          UpdateDollarStoreEntryRequest _updateRequest = new();

    private bool _waitingForRefresh;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (State == ModificationState.Reading && DollarStoreEntry != null)
            _waitingForRefresh = false;

        _createRequest.ServiceId = ServiceId ?? Guid.Empty;
        
        if (DollarStoreEntry != null && !_waitingForRefresh)
        {
            _updateRequest = new UpdateDollarStoreEntryRequest
            {
                Guid = DollarStoreEntry.Id
            };

            _updateRequest.DollarDoosMade.SetInitialValue(DollarStoreEntry.DollarDoosMade);
            _updateRequest.Notes.SetInitialValue(DollarStoreEntry.Notes);
        }
        else if (State != ModificationState.Creating)
        {
            _waitingForRefresh = true;
        }
    }

    public async Task<bool> CreateDollarStoreEntry()
    {
        _waitingForRefresh = true;
        StateHasChanged();
        BasicResponse? resp = await DollarStoreEntryService.Create(_createRequest);

        if (resp.HasErrorOrNull())
        {
            _waitingForRefresh = false;
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        return true;
    }

    public async Task<bool> UpdateDollarStoreEntry()
    {
        if (_updateRequest.Guid == Guid.Empty)
            return false;

        _waitingForRefresh = true;
        StateHasChanged();
        BasicResponse? resp = await DollarStoreEntryService.Update(_updateRequest);

        if (resp.HasErrorOrNull())
        {
            _waitingForRefresh = false;
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        return true;
    }
}