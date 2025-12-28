using System.Globalization;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Calendar.Models;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Calendar.Pages;

public partial class CalendarPage : EventListeningComponent
{
    [SupplyParameterFromQuery]
    public string? Date { get; set; }

    private DateTime? _dateTime = DateTime.Now;
    private DateTime  CalendarDate => _dateTime ?? DateTime.Now;

    private ICollection<CalendarTerm>?  _calendarTerms;
    private ICollection<CalendarEvent>? _events;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (Date != null && DateTime.TryParseExact(
                Date,
                "MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date)
           )
            _dateTime = date;

        await Task.WhenAll(
            RefreshSchoolTerms(),
            RefreshServices(),
            SubscribeToEvent(SchoolTerm.BuildSubscription(), RefreshSchoolTerms),
            SubscribeToEvent(Service.BuildSubscription(), RefreshServices),
            SubscribeToEvent(ServiceType.BuildSubscription(), RefreshServices)
        );
    }

    private CancellationTokenSource _refreshSchoolTermsTokenSource = new();

    private async Task RefreshSchoolTerms()
    {
        await _refreshSchoolTermsTokenSource.CancelAsync();
        _refreshSchoolTermsTokenSource = new CancellationTokenSource();

        BasicReadMultipleResponse<SchoolTerm>? response = await SchoolTermsService.ReadMultiple(
            new SchoolTermsRequest
            {
                Pagination = PaginationRequest.All(),
                Year = CalendarDate.Year
            },
            _refreshSchoolTermsTokenSource.Token
        );

        _calendarTerms = response?.Entities
            .Select(x => new CalendarTerm
                {
                    StartDate = x.LocalStartDate,
                    EndDate = x.LocalEndDate,
                    Name = x.Name,
                    Color = null
                }
            )
            .ToList();

        StateHasChanged();

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
    }

    private CancellationTokenSource _refreshServicesTokenSource = new();

    private async Task RefreshServices()
    {
        await _refreshServicesTokenSource.CancelAsync();
        _refreshServicesTokenSource = new CancellationTokenSource();

        IReadMultipleResponse<Service>? response = await ServicesService.ReadMultiple(
            new ServicesRequest
            {
                Pagination = PaginationRequest.All(),
                Year = CalendarDate.Year
            },
            _refreshServicesTokenSource.Token
        );

        _events = response?.Entities
            .Select(x => new CalendarEvent
                {
                    Date = x.LocalDate,
                    Name = x.Name ?? "Service", // TODO: x.ServiceType?.Label ?? "Service",
                    Color = null, //TODO: x.ServiceType?.Color,
                    Href = $"/Services/{x.Id}"
                }
            )
            .ToList();

        StateHasChanged();

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
    }

    private async Task DateChanged(DateTime? dateTime)
    {
        if (dateTime?.Year != CalendarDate.Year)
        {
            _dateTime = dateTime;
            await Task.WhenAll(RefreshSchoolTerms(), RefreshServices());
        }

        _dateTime = dateTime;
        SetQueryParameters();
    }

    private void SetQueryParameters()
    {
        Navigation.NavigateTo(GetQueryParameters());
    }

    private string GetQueryParameters()
    {
        return Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [nameof(Date)] = $"{_dateTime:MM-yyyy}"
        });
    }
}