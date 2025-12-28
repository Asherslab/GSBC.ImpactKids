using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Features.ServiceTypes.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Features.ServiceTypes.Pages;

public partial class Multiple
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        SubscribeToSelector(s => s.Search, _ => UpdateFilteredServiceTypes());
        ServiceTypesStore.Subscribe(_ => UpdateFilteredServiceTypes());

        await Task.WhenAll(
            ServiceTypesStore.RefreshAll(),
            UpdateFilteredServiceTypes()
        );
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
                return s.SetSearch(nullableText);
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
            await Update(s => s with { FilteredServiceTypes = s.FilteredServiceTypes.ToLoading() });

            bool success = await _serviceTypeDetails.CreateServiceType();
            _showCreateDialog = !success;

            if (!success)
                await UpdateFilteredServiceTypes();
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
            await Update(s => s with { FilteredServiceTypes = s.FilteredServiceTypes.ToLoading() });

            bool success = await _serviceTypeDetails.UpdateServiceType();
            _showUpdateDialog = !success;

            if (!success)
                await UpdateFilteredServiceTypes();
        }
    }

    private async Task ShowDeleteServiceType(ServiceType serviceType)
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;

        BasicReadRequest request = new()
        {
            Guid = serviceType.Id
        };

        await Update(s => s with { FilteredServiceTypes = s.FilteredServiceTypes.ToLoading() });

        BasicResponse resp = await ServiceTypesService.Delete(request);

        if (resp.HasErrorOrNull())
            await UpdateFilteredServiceTypes();
    }
}