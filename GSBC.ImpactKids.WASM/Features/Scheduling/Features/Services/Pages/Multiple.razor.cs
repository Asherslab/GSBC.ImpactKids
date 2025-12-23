using EasyAppDev.Blazor.Store.Blazor;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Pages;

public partial class Multiple : StoreComponentWithUtilities<MultipleServicesState>
{
    private ICollection<SchoolTerm>? _schoolTerms;

    // private ICollection<ServiceType>? _serviceTypes;
    private ICollection<Service>? _services;

    private readonly Dictionary<int, ICollection<Service>?> _quarters = new()
    {
        { 1, null },
        { 2, null },
        { 3, null },
        { 4, null },
    };

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (ServiceTypesStore.GetState().Entities.IsNotAsked)
        {
            await ServiceTypesStore.RefreshAll();
        }

        // await Task.WhenAll(
        //     RefreshServiceTypes()
        //     // SubscribeToEvent(Service.BuildSubscription(), RefreshServices),
        //     // SubscribeToEvent(SchoolTerm.BuildSubscription(), RefreshSchoolTerms),
        //     // SubscribeToEvent(Shared.Contracts.Entities.Features.Scheduling.ServiceType.BuildSubscription(),
        //     //     RefreshServiceTypes)
        // );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        await Task.WhenAll(
            RefreshServices(),
            RefreshSchoolTerms()
        );
    }

    private CancellationTokenSource _refreshSchoolTermsTokenSource = new();

    private async Task RefreshSchoolTerms()
    {
        await _refreshSchoolTermsTokenSource.CancelAsync();
        _refreshSchoolTermsTokenSource = new CancellationTokenSource();

        int year = State.Date?.Year ?? DateTime.Now.Year;
        BasicReadMultipleResponse<SchoolTerm>? response = await SchoolTermsService.ReadMultiple(
            new SchoolTermsRequest
            {
                Pagination = PaginationRequest.All(),
                Year = year
            },
            _refreshSchoolTermsTokenSource.Token
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _schoolTerms = response.Entities;
        StateHasChanged();
    }

    private CancellationTokenSource _refreshServicesTokenSource = new();

    private async Task RefreshServices()
    {
        await _refreshServicesTokenSource.CancelAsync();
        _refreshServicesTokenSource = new CancellationTokenSource();

        int year = State.Date?.Year ?? DateTime.Now.Year;
        BasicReadMultipleResponse<Service>? response = await ServicesService.ReadMultiple(
            new ServicesRequest
            {
                Pagination = PaginationRequest.All(),
                Year = year,
                ServiceTypeId = State.ServiceType
            },
            _refreshServicesTokenSource.Token
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _services = response.Entities;
        foreach (int i in Enumerable.Range(0, 4))
        {
            DateTime startDate = new(year, i * 3 + 1, 1);
            DateTime endDate   = new(year, (i + 1) * 3, DateTime.DaysInMonth(year, (i + 1) * 3));

            ICollection<Service> servicesForThisQuarter = response.Entities
                .Where(x => x.LocalDate >= startDate && x.LocalDate <= endDate)
                .ToList();

            _quarters[i + 1] = servicesForThisQuarter;
        }

        StateHasChanged();
    }

    private List<Service>? GetServicesForTerm(SchoolTerm term) =>
        _services?.Where(x => x.SchoolTerm?.Id == term.Id).ToList();

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

    private ServiceDetails? _serviceDetails;

    private bool _showCreateDialog;

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