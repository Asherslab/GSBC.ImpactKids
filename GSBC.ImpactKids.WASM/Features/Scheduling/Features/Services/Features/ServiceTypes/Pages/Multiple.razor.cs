using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Features.ServiceTypes.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Features.ServiceTypes.Pages;

public partial class Multiple : EventListeningComponent
{
    private string? _search;

    private ICollection<ServiceType>? _serviceTypes;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            RefreshServiceTypes(),
            SubscribeToEvent(ServiceType.BuildSubscription(), RefreshServiceTypes)
        );
    }

    private CancellationTokenSource _refreshServiceTypesTokenSource = new();

    private async Task RefreshServiceTypes()
    {
        await _refreshServiceTypesTokenSource.CancelAsync();
        _refreshServiceTypesTokenSource = new CancellationTokenSource();

        BasicReadMultipleResponse<ServiceType>? response = await ServiceTypeService.ReadMultiple(
            new BasicReadMultipleRequest
            {
                SearchString = _search
            },
            _refreshServiceTypesTokenSource.Token
        );

        _serviceTypes = response?.Entities;
        StateHasChanged();

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
    }

    private async Task OnSearch(string text)
    {
        _search = text;
        if (string.IsNullOrWhiteSpace(_search))
            _search = null;
        await RefreshServiceTypes();
    }

    private ServiceTypeDetails? _serviceTypeDetails;

    private bool _showCreateDialog;

    private async Task CreateServiceType()
    {
        if (_serviceTypeDetails != null)
        {
            bool success = await _serviceTypeDetails.CreateServiceType();
            _showCreateDialog = !success;
        }
    }

    private bool         _showUpdateDialog;
    private ServiceType? _updatingServiceType;

    private void ShowUpdateServiceType(ServiceType serviceType)
    {
        _updatingServiceType = serviceType;
        _showUpdateDialog = true;
    }

    private async Task UpdateServiceType()
    {
        if (_serviceTypeDetails != null)
        {
            bool success = await _serviceTypeDetails.UpdateServiceType();
            _showUpdateDialog = !success;
        }
    }
}