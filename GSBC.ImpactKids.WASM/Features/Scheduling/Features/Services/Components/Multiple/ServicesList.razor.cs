using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Multiple;

public partial class ServicesList : ComponentBase
{
    [Parameter]
    public Func<Service, bool>? Filter { get; set; }

    [Parameter]
    public int? Quarter { get; set; }

    [Parameter]
    public int? Year { get; set; }

    [Parameter]
    public Guid? SchoolTermId { get; set; }
    
    [Parameter]
    public bool ShowFakes { get; set; }

    private AsyncData<ImmutableList<Guid>> _serviceIds = AsyncData<ImmutableList<Guid>>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ServicesStore.Subscribe(_ => FilterServices());

        await Task.WhenAll(
            ServicesStore.RefreshAll()
        );
        FilterServices();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        FilterServices();
    }

    private void FilterServices()
    {
        AsyncData<ImmutableList<Service>> services = ServicesStore.GetState().Entities;

        if (services.Data == null)
        {
            _serviceIds = _serviceIds.CopyStatus(services);
            return;
        }

        IEnumerable<Service> filteredServices = services.Data;

        if (Filter != null)
        {
            filteredServices = filteredServices
                .Where(Filter);
        }

        if (Year != null && Quarter != null)
        {
            filteredServices = filteredServices
                .Where(x =>
                    x.LocalDate >= GetStartDateForQuarter(Year.Value, Quarter.Value) &&
                    x.LocalDate <= GetEndDateForQuarter(Year.Value, Quarter.Value)
                );
        }

        if (SchoolTermId != null)
        {
            filteredServices = filteredServices
                .Where(x => x.SchoolTermId == SchoolTermId);
        }

        _serviceIds = _serviceIds.ToSuccess(filteredServices
            .Select(x => x.Id)
            .ToImmutableList()
        );

        StateHasChanged();
    }

    private static DateTime GetStartDateForQuarter(int year, int quarter) =>
        new(year, (quarter - 1) * 3 + 1, 1);

    private static DateTime GetEndDateForQuarter(int year, int quarter) =>
        new(
            year,
            quarter * 3,
            DateTime.DaysInMonth(year, quarter * 3)
        );
}