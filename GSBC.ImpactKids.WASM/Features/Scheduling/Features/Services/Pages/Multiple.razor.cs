using System.Globalization;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Pages;

public partial class Multiple : EventListeningComponent
{
    [SupplyParameterFromQuery]
    public string? Year { get; set; }

    [SupplyParameterFromQuery]
    public string? Display { get; set; }

    [SupplyParameterFromForm]
    public Guid? ServiceType { get; set; }

    private DateTime?             _date    = DateTime.Now;
    private ServiceDisplayOptions _display = ServiceDisplayOptions.Quarters;

    private ICollection<SchoolTerm>?  _schoolTerms;
    private ICollection<ServiceType>? _serviceTypes;
    private ICollection<Service>?     _services;

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

        await Task.WhenAll(
            RefreshServiceTypes(),
            SubscribeToEvent(Service.BuildSubscription(), RefreshServices),
            SubscribeToEvent(SchoolTerm.BuildSubscription(), RefreshSchoolTerms),
            SubscribeToEvent(Shared.Contracts.Entities.Features.Scheduling.ServiceType.BuildSubscription(),
                RefreshServiceTypes)
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (Year != null && DateTime.TryParseExact(
                Year,
                "yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date)
           )
            _date = date;

        if (Display != null && Enum.TryParse(Display, out ServiceDisplayOptions display))
            _display = display;

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

        int year = _date?.Year ?? DateTime.Now.Year;
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

    private CancellationTokenSource _refreshServiceTypesTokenSource = new();

    private async Task RefreshServiceTypes()
    {
        await _refreshServiceTypesTokenSource.CancelAsync();
        _refreshServiceTypesTokenSource = new CancellationTokenSource();

        BasicReadMultipleResponse<ServiceType>? response = await ServiceTypeService.ReadMultiple(
            new BasicReadMultipleRequest
            {
                Pagination = PaginationRequest.All()
            },
            _refreshServiceTypesTokenSource.Token
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _serviceTypes = response.Entities;
        StateHasChanged();
    }

    private CancellationTokenSource _refreshServicesTokenSource = new();

    private async Task RefreshServices()
    {
        await _refreshServicesTokenSource.CancelAsync();
        _refreshServicesTokenSource = new CancellationTokenSource();

        int year = _date?.Year ?? DateTime.Now.Year;
        BasicReadMultipleResponse<Service>? response = await ServicesService.ReadMultiple(
            new ServicesRequest
            {
                Pagination = PaginationRequest.All(),
                Year = year,
                ServiceTypeId = ServiceType
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
                .Where(x => x.Date >= startDate && x.Date <= endDate)
                .ToList();

            _quarters[i + 1] = servicesForThisQuarter;
        }
        StateHasChanged();
    }

    private List<Service>? GetServicesForTerm(SchoolTerm term) =>
        _services?.Where(x => x.SchoolTerm?.Id == term.Id).ToList();

    private void OnDateChanged(DateTime? dateTime)
    {
        _date = dateTime;
        SetQueryParameters();
    }

    private void DisplayChanged(ServiceDisplayOptions display)
    {
        _display = display;
        SetQueryParameters();
    }
    
    private void ServiceTypeChanged(Guid? serviceTypeId)
    {
        ServiceType = serviceTypeId;
        SetQueryParameters();
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

    private void SetQueryParameters()
    {
        Navigation.NavigateTo(GetQueryParameters());
    }

    private string GetQueryParameters()
    {
        return Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [nameof(Year)] = $"{_date:yyyy}",
            [nameof(Display)] = $"{_display}",
            [nameof(ServiceType)] = ServiceType
        });
    }
}

public enum ServiceDisplayOptions
{
    Quarters,
    SchoolTerms
}