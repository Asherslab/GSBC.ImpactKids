using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Pages;

public partial class Individual
{
    [Parameter]
    public Guid? Id { get; set; }

    [SupplyParameterFromQuery]
    public bool Previous { get; set; }

    [SupplyParameterFromQuery]
    public bool Upcoming { get; set; }

    private AsyncData<Service> _service = AsyncData<Service>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ServicesStore.Subscribe(_ => RetrieveService());

        await Task.WhenAll(
            ServicesStore.RefreshAll()
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
            StateHasChanged();
            return;
        }

        Service? service = null;
        if (Id != null)
            service = services.Data!
                .FirstOrDefault(x => x.Id == Id);
        else if (Previous)
            service = services.Data!
                .OrderByDescending(x => x.LocalDate)
                .FirstOrDefault(x => x.LocalDate.Date <= DateTime.Now.Date);
        else if (Upcoming)
            service = services.Data!
                .OrderBy(x => x.LocalDate)
                .FirstOrDefault(x => x.LocalDate.Date >= DateTime.Now.Date);

        _service = service == null
            ? _service.ToFailure("Failed to find Service")
            : _service.ToSuccess(service);
    }
}