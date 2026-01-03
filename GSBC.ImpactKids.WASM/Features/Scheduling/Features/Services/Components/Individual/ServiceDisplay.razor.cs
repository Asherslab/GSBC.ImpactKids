using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

public partial class ServiceDisplay
{
    private AsyncData<ServiceType?> _serviceType = AsyncData<ServiceType?>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ServiceTypesStore.Subscribe(_ => RetrieveEntity());

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            ServiceTypesStore.RefreshAll()
        );
    }

    protected override void OnRetrievedEntity()
    {
        if (Entity.Data!.ServiceTypeId == null)
        {
            _serviceType = _serviceType.ToSuccess(null);
            return;
        }

        _serviceType = ServiceTypesStore.GetState().First(x => x.Id == Entity.Data!.ServiceTypeId);
    }
}