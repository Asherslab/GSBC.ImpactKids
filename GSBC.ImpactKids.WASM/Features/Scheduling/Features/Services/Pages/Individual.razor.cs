using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Pages;

public partial class Individual
{
    [Parameter]
    public Guid Id { get; set; }

    private Service?                  _service;
    private ICollection<MemoryVerse>? _memoryVerses;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        // bool entityChanged = Id != _service?.Id;

        await Task.WhenAll(
            RefreshService(),
            RefreshMemoryVerses(),
            SubscribeToEvent(MemoryVerse.BuildSubscription(), RefreshMemoryVerses),
            SubscribeToEvent(Service.BuildSubscription(serviceId: Id), RefreshService),
            SubscribeToEvent(DollarStoreEntry.BuildSubscription(serviceId: Id), RefreshService)
        );
    }

    private CancellationTokenSource _refreshServiceTokenSource = new();

    private async Task RefreshService()
    {
        _service = null;
        StateHasChanged();

        await _refreshServiceTokenSource.CancelAsync();
        _refreshServiceTokenSource = new CancellationTokenSource();

        BasicReadResponse<Service>? response = await ServicesService.Read(
            new ServiceRequest
            {
                Guid = Id
            },
            _refreshServiceTokenSource.Token
        );

        _service = response?.Entity;
        StateHasChanged();

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
    }

    private CancellationTokenSource _refreshMemoryVersesTokenSource = new();

    private async Task RefreshMemoryVerses()
    {
        _memoryVerses = null;
        StateHasChanged();

        await _refreshMemoryVersesTokenSource.CancelAsync();
        _refreshMemoryVersesTokenSource = new CancellationTokenSource();

        BasicReadMultipleResponse<MemoryVerse>? response = await MemoryVersesService.ReadMultiple(
            new MemoryVersesRequest
            {
                ServiceId = Id
            },
            _refreshMemoryVersesTokenSource.Token
        );

        _memoryVerses = response?.Entities;
        StateHasChanged();

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
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
        Navigation.NavigateTo("/Services");
    }

    private async Task DeleteDollarStoreEntry()
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null || _service?.DollarStoreEntry?.Id == null)
            return;

        BasicReadRequest request = new()
        {
            Guid = _service.DollarStoreEntry.Id
        };

        await Unbind(); // unbinds this component from events first, so that we don't get a refresh after deleting before navigating
        await DollarStoreEntryService.Delete(request);
    }
}