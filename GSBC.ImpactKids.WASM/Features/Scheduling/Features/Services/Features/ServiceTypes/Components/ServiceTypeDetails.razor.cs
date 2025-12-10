using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services.ServiceTypes;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Features.ServiceTypes.Components;

public partial class ServiceTypeDetails : ComponentBase
{
    [Parameter]
    public ServiceType? ServiceType { get; set; }

    [Parameter]
    public ModificationState State { get; set; }

    private readonly CreateServiceTypeRequest _createRequest = new();
    private          UpdateServiceTypeRequest _updateRequest = new();

    private bool _waitingForRefresh;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (ServiceType != null)
        {
            _updateRequest = new UpdateServiceTypeRequest
            {
                Guid = ServiceType.Id
            };

            _updateRequest.Label.SetInitialValue(ServiceType.Label);
            _updateRequest.Color.SetInitialValue(ServiceType.Color);

            _waitingForRefresh = false;
        }
        else if (State != ModificationState.Creating)
        {
            _waitingForRefresh = true;
        }
    }

    public async Task<bool> CreateServiceType()
    {
        _waitingForRefresh = true;
        StateHasChanged();
        BasicResponse? resp = await ServiceTypeService.Create(_createRequest);

        if (resp.HasErrorOrNull())
        {
            _waitingForRefresh = false;
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        StateHasChanged();
        return true;
    }

    public async Task<bool> UpdateServiceType()
    {
        if (_updateRequest.Guid == Guid.Empty)
            return false;

        _waitingForRefresh = true;
        StateHasChanged();
        BasicResponse? resp = await ServiceTypeService.Update(_updateRequest);

        if (resp.HasErrorOrNull())
        {
            _waitingForRefresh = false;
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        StateHasChanged();
        return true;
    }
}

public enum ModificationState
{
    Creating,
    Reading,
    Updating
}