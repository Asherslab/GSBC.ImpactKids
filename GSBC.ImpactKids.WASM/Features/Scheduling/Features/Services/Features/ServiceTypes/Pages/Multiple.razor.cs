using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Features.ServiceTypes.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Features.ServiceTypes.Pages;

public partial class Multiple
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (ServiceTypesStore.GetState().Entities.IsNotAsked)
            await ServiceTypesStore.RefreshAll();

        if (State.FilteredServiceTypes.IsNotAsked)
            await UpdateFilteredServiceTypes();
        
        SubscribeToSelector(s => s.Search, _ => UpdateFilteredServiceTypes());
    }

    private Task UpdateFilteredServiceTypes()
    {
        AsyncData<ImmutableList<ServiceType>> serviceTypes = ServiceTypesStore.GetState().Entities;

        if (!serviceTypes.HasData)
            return Update(s => s with { FilteredServiceTypes = serviceTypes });

        return Update(s => s with
        {
            FilteredServiceTypes = s.FilteredServiceTypes.ToSuccess(
                serviceTypes.Data!
                    .Where(x => x.Label.Contains(State.Search ?? "", StringComparison.InvariantCultureIgnoreCase))
                    .ToImmutableList()
            )
        });
    }

    private async Task OnSearch(string text)
    {
        await UpdateDebounced(s =>
            {
                string? nullableText = text;
                if (string.IsNullOrWhiteSpace(nullableText))
                    nullableText = null;
                return s with { Search = nullableText };
            },
            TimeSpan.FromSeconds(0.25).Milliseconds
        );
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