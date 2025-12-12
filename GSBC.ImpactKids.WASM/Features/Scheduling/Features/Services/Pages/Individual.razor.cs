using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Pages;

public partial class Individual
{
    [Parameter]
    public Guid Id { get; set; }

    private Service? _service;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        // bool entityChanged = Id != _service?.Id;

        await Task.WhenAll(
            RefreshService(),
            SubscribeToEvent(Service.BuildSubscription(serviceId: Id), RefreshService)
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
}