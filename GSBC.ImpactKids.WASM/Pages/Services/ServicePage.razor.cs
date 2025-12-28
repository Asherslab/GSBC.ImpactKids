using GSBC.ImpactKids.Shared.Contracts;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Components.Dialogs.Create;
using GSBC.ImpactKids.WASM.Components.Dialogs.Update;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Pages.Services;

public partial class ServicePage : EventListeningComponent
{
    [SupplyParameterFromQuery]
    public Guid? Id { get; set; }

    [SupplyParameterFromQuery]
    public bool Previous { get; set; }

    [SupplyParameterFromQuery]
    public bool Upcoming { get; set; }

    private Service?                  _service;
    private DollarStoreEntry?         _dollarStoreEntry;
    private ICollection<MemoryVerse>? _memoryVerses;

    // use OnParametersSet because changing between pages of different query values does not reload with OnInitialized
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        await RefreshService();
        await SubscribeToEvent(Service.BuildSubscription(_service?.SchoolTermId, _service?.Id), RefreshService);

        await Task.WhenAll(RefreshMemoryVerses(), RefreshDollarStoreEntry());
        await SubscribeToEvent(MemoryVerse.BuildSubscription(), RefreshMemoryVerses);
        await SubscribeToEvent(DollarStoreEntry.BuildSubscription(serviceId: _service?.Id), RefreshDollarStoreEntry);
    }

    private async Task RefreshService()
    {
        BasicReadResponse<Service>? response = await ServicesService.Read(
            new ServiceRequest
            {
                Guid = Id ?? Guid.Empty,
                PreviousService = Previous,
                UpcomingService = Upcoming
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _service = response.Entity;
        StateHasChanged();
    }

    private async Task RefreshDollarStoreEntry()
    {
        if (_service == null)
            return;

        BasicReadResponse<DollarStoreEntry>? response = await DollarStoreEntryService.Read(
            new BasicReadRequest
            {
                Guid = _service.Id
            }
        );

        _dollarStoreEntry = response?.Entity;
        StateHasChanged();
        
        if (response.HasErrorOrNull())
        {
            if (response?.Error == ErrorConstants.DollarStoreEntryNotFound)
                return; // this is fine dollar store entry is optional
            Snackbar.AddErrorResponse(response);
        }
    }

    private async Task RefreshMemoryVerses()
    {
        if (_service == null)
            return;

        BasicReadMultipleResponse<MemoryVerse>? response = await MemoryVersesService.ReadMultiple(
            new MemoryVersesRequest
            {
                ServiceId = _service.Id
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _memoryVerses = response.Entities;
        StateHasChanged();
    }

    private async Task UpdateService()
    {
        DialogParameters<UpdateServiceDialog> parameters = new()
        {
            { x => x.Service, _service }
        };

        await DialogService.ShowAsync<UpdateServiceDialog>("Update Service", parameters);
    }

    private async Task DeleteService()
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null || _service == null)
            return;

        BasicReadRequest request = new()
        {
            Guid = _service.Id
        };

        await Unbind(); // unbinds this component from events first, so that we don't get a refresh after deleting before navigating
        await ServicesService.Delete(request);
        Navigation.NavigateTo("/terms");
    }

    private async Task CreateDollarStoreEntry()
    {
        if (_service == null)
            return;

        DialogParameters<CreateDollarStoreEntryDialog> parameters = new()
        {
            { x => x.ServiceId, _service.Id }
        };

        await DialogService.ShowAsync<CreateDollarStoreEntryDialog>(
            $"Create Dollar Store Entry for: {_service.GetDisplayName()}", parameters);
    }

    private async Task UpdateDollarStoreEntry()
    {
        if (_service == null || _dollarStoreEntry == null)
            return;

        DialogParameters<UpdateDollarStoreEntryDialog> parameters = new()
        {
            { x => x.Entry, _dollarStoreEntry },
            { x => x.ServiceId, _service.Id }
        };

        await DialogService.ShowAsync<UpdateDollarStoreEntryDialog>(
            $"Update Dollar Store Entry for: {_service.GetDisplayName()}", parameters);
    }

    private async Task DeleteDollarStoreEntry()
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null || _dollarStoreEntry == null)
            return;

        BasicReadRequest request = new()
        {
            Guid = _dollarStoreEntry.Id
        };

        await DollarStoreEntryService.Delete(request);
    }

    private string PageName()
    {
        if (_service?.Name != null)
            return _service.Name;
        if (Previous)
            return "Previous Service";
        if (Upcoming)
            return "Upcoming Service";
        return "Service";
    }
}