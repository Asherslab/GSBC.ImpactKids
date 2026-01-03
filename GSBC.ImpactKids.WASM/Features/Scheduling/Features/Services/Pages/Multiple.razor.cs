using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Blazor;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.School.Components.Individual;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Pages;

public partial class Multiple : StoreComponentWithUtilities<MultipleServicesState>
{
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

    private Func<Service, bool> ServiceFilterForQuarters(int quarter) =>
        x => x.LocalDate >= GetStartDateForQuarter(GetYear(), quarter) &&
             x.LocalDate <= GetEndDateForQuarter(GetYear(), quarter) && (
                 State.ServiceType == null ||
                 x.ServiceTypeId == State.ServiceType
             );

    private Func<Service, bool> ServiceFilterForSchoolTerm(Guid schoolTermId) =>
        x => x.SchoolTermId == schoolTermId && (
            State.ServiceType == null ||
            x.ServiceTypeId == State.ServiceType
        );

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

    private async Task CreateSchoolTerm() =>
        await DetailsComponentDialog.Open<SchoolTermDetails>(DialogService, "Create School Term", ModificationState.Creating);
    
    private async Task CreateService() =>
        await DetailsComponentDialog.Open<ServiceDetails>(DialogService, "Create Service", ModificationState.Creating);

    private static DateTime GetStartDateForQuarter(int year, int quarter) =>
        new(year, (quarter - 1) * 3 + 1, 1);

    private static DateTime GetEndDateForQuarter(int year, int quarter) =>
        new(
            year,
            quarter * 3,
            DateTime.DaysInMonth(year, quarter * 3),
            23,
            59,
            59
        );
}

public enum ServiceDisplayOptions
{
    Quarters,
    SchoolTerms
}