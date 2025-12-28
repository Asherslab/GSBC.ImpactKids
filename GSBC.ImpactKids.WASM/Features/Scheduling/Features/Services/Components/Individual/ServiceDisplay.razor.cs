using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

public partial class ServiceDisplay
{
    [Parameter]
    public required Guid? Id { get; set; }

    private AsyncData<Service>      _service     = AsyncData<Service>.NotAsked();
    private AsyncData<ServiceType?> _serviceType = AsyncData<ServiceType?>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ServicesStore.Subscribe(_ => RetrieveService());
        ServiceTypesStore.Subscribe(_ => RetrieveServiceType());

        await Task.WhenAll(
            ServicesStore.RefreshAll(),
            ServiceTypesStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        RetrieveService();
    }

    private void RetrieveService()
    {
        AsyncData<ImmutableList<Service>> services = ServicesStore.GetState().Entities;

        if (!services.HasData)
        {
            _service = _service.CopyStatus(services);
            return;
        }

        Service? service = services.Data!
            .FirstOrDefault(x => x.Id == Id);

        _service = service == null
            ? _service.ToFailure("Failed to find Service")
            : _service.ToSuccess(service);

        RetrieveServiceType();
    }

    private void RetrieveServiceType()
    {
        if (!_service.HasData)
            return;

        if (_service.Data!.ServiceTypeId == null)
        {
            _serviceType = _serviceType.ToSuccess(null);
            StateHasChanged();
            return;
        }

        AsyncData<ImmutableList<ServiceType>> serviceTypes = ServiceTypesStore.GetState().Entities;

        if (!serviceTypes.HasData)
        {
            _serviceType = _serviceType.CopyStatus(serviceTypes);
            StateHasChanged();
            return;
        }

        ServiceType? serviceType = serviceTypes.Data!
            .FirstOrDefault(x => x.Id == _service.Data!.ServiceTypeId);

        _serviceType = serviceType == null
            ? _serviceType.ToFailure("Failed to find Service Type")
            : _serviceType.ToSuccess(serviceType);

        StateHasChanged();
    }
}