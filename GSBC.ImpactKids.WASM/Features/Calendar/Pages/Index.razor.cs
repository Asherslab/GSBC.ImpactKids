using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Calendar.Models;

namespace GSBC.ImpactKids.WASM.Features.Calendar.Pages;

public partial class Index : EventListeningComponent
{
    private DateTime? _dateTime = DateTime.Now;
    private DateTime  CalendarDate => _dateTime ?? DateTime.Now;

    private ICollection<CalendarTerm>?  _calendarTerms;
    private ICollection<CalendarEvent>? _events;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        await Task.WhenAll(
            RefreshSchoolTerms(),
            RefreshServices(),
            SubscribeToEvent(SchoolTerm.BuildSubscription(), RefreshSchoolTerms),
            SubscribeToEvent(Service.BuildSubscription(), RefreshServices)
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
                Year = CalendarDate.Year
            },
            _refreshSchoolTermsTokenSource.Token
        );

        _calendarTerms = response?.Entities
            .Select(x => new CalendarTerm
                {
                    StartDate = x.StartDate,
                    EndDate = x.EndDate,
                    Name = x.Name,
                    Color = "darkgreen"
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

        BasicReadMultipleResponse<Service>? response = await ServicesService.ReadMultiple(
            new ServicesRequest
            {
                Year = CalendarDate.Year
            },
            _refreshServicesTokenSource.Token
        );

        _events = response?.Entities
            .Select(x => new CalendarEvent
                {
                    Date = x.Date,
                    Name = x.Name ?? "Impact Kids",
                    Color = "darkcyan"
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
        Console.WriteLine($"{dateTime} | {CalendarDate}");
        if (dateTime?.Year != CalendarDate.Year)
        {
            _dateTime = dateTime;
            await Task.WhenAll(RefreshSchoolTerms(), RefreshServices());
        }

        _dateTime = dateTime;
        StateHasChanged();
    }
}