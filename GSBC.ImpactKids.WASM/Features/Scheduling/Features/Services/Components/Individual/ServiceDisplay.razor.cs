using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
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
        _service = ServicesStore.GetState().First(x => x.Id == Id);
        StateHasChanged();

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

        _serviceType = ServiceTypesStore.GetState().First(x => x.Id == _service.Data!.ServiceTypeId);
        StateHasChanged();
    }
}