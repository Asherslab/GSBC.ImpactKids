using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Blazor;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.School.Components.Individual;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Pages;

public partial class Multiple : StoreComponentWithUtilities<MultipleServicesState>
{
    // filter services by selected service type
    private Func<Service, bool> ServiceFilter => x => State.ServiceType == null || x.ServiceTypeId == State.ServiceType;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        SchoolTermsStore.Subscribe(_ => UpdateFilteredSchoolTerms());
        ServiceTypesStore.Subscribe(_ => StateHasChanged());

        SubscribeToSelector(x => x.Date, _ => UpdateFilteredSchoolTerms());

        await Task.WhenAll(
            SchoolTermsStore.RefreshAll(),
            ServiceTypesStore.RefreshAll()
        );

        UpdateFilteredSchoolTerms();
    }

    private void UpdateFilteredSchoolTerms()
    {
        AsyncData<ImmutableList<SchoolTerm>> schoolTerms = SchoolTermsStore.GetState().Entities;

        if (schoolTerms.Data == null)
        {
            Update(s => s with { FilteredSchoolTerms = s.FilteredSchoolTerms.CopyStatus(schoolTerms) });
            return;
        }

        int year = State.Date?.Year ?? DateTime.Now.Year;
        Update(s => s with
        {
            FilteredSchoolTerms = s.FilteredSchoolTerms.ToSuccess(
                schoolTerms.Data
                    .Where(x => x.LocalStartDate.Year == year || x.LocalEndDate.Year == year)
                    .ToImmutableList()
            )
        });
    }


    private int GetYear() => State.Date?.Year ?? DateTime.Now.Year;

    private void OnDateChanged(DateTime? dateTime)
    {
        Update(x => x.SetDate(dateTime));
    }

    private void DisplayChanged(ServiceDisplayOptions display)
    {
        Update(x => x.SetDisplay(display));
    }

    private void ServiceTypeChanged(Guid? serviceTypeId)
    {
        Update(x => x.SetServiceType(serviceTypeId));
    }
    
    private SchoolTermDetails? _schoolTermDetails;
    private bool               _showCreateSchoolTermDialog;

    private async Task CreateSchoolTerm()
    {
        if (_schoolTermDetails != null)
        {
            bool success = await _schoolTermDetails.CreateSchoolTerm();
            _showCreateSchoolTermDialog = !success;
        }
    }

    private ServiceDetails? _serviceDetails;
    private bool            _showCreateDialog;

    private async Task CreateService()
    {
        if (_serviceDetails != null)
        {
            bool success = await _serviceDetails.CreateService();
            _showCreateDialog = !success;
        }
    }
}

public enum ServiceDisplayOptions
{
    Quarters,
    SchoolTerms
}