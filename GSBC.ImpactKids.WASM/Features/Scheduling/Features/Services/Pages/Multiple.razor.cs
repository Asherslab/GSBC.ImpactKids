using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Extensions;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Pages;

public partial class Multiple : EventListeningComponent
{
    private DateTime?             _date           = DateTime.Now;
    private ServiceDisplayOptions _displayOptions = ServiceDisplayOptions.Quarters;

    private ICollection<SchoolTerm>? _schoolTerms;
    private ICollection<Service>?    _services;

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
            RefreshServices(),
            RefreshSchoolTerms(),
            SubscribeToEvent(Service.BuildSubscription(), RefreshServices),
            SubscribeToEvent(SchoolTerm.BuildSubscription(), RefreshSchoolTerms)
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
                Year = year
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

    private async Task OnDateChanged(DateTime? dateTime)
    {
        if (_date?.Year != dateTime?.Year)
        {
            _date = dateTime;
            await Task.WhenAll(
                RefreshServices(),
                RefreshSchoolTerms()
            );
        }
        _date = dateTime;
    }
}

public enum ServiceDisplayOptions
{
    Quarters,
    SchoolTerms
}