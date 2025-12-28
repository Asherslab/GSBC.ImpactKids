using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.WASM.Extensions;

namespace GSBC.ImpactKids.WASM.Features.Calendar.Pages;

public partial class CalendarPage
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        SchoolTermsStore.Subscribe(_ => UpdateCalendarTerms());
        ServicesStore.Subscribe(_ => UpdateCalendarEvents());
        ServiceTypesStore.Subscribe(_ => UpdateCalendarEvents());

        await Task.WhenAll(
            SchoolTermsStore.RefreshAll(),
            ServicesStore.RefreshAll(),
            ServiceTypesStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        UpdateCalendarTerms();
        UpdateCalendarEvents();
    }

    private void UpdateCalendarTerms()
    {
        AsyncData<ImmutableList<SchoolTerm>> terms = SchoolTermsStore.GetState().Entities;

        if (!terms.HasData)
        {
            Update(s => s with { Terms = s.Terms.CopyStatus(terms) });
            return;
        }

        ImmutableList<CalendarTerm> calendarTerms = terms.Data!
            .Select(x => new CalendarTerm(
                    x.LocalStartDate,
                    x.LocalEndDate,
                    x.Name,
                    null
                )
            ).ToImmutableList();

        Update(s => s with { Terms = s.Terms.ToSuccess(calendarTerms) });
    }

    private void UpdateCalendarEvents()
    {
        AsyncData<ImmutableList<Service>>     services     = ServicesStore.GetState().Entities;
        AsyncData<ImmutableList<ServiceType>> serviceTypes = ServiceTypesStore.GetState().Entities;

        if (!services.HasData)
        {
            Update(s => s with { Events = s.Events.CopyStatus(services) });
            return;
        }

        ImmutableList<CalendarEvent> calendarEvents = services.Data!
            .Select(x => new
            {
                Service = x,
                ServiceType = serviceTypes.Data?.FirstOrDefault(y => y.Id == x.ServiceTypeId)
            })
            .Select(x => new CalendarEvent(
                    x.Service.LocalDate,
                    x.Service.Name ?? x.ServiceType?.Label ?? "Service",
                    x.ServiceType?.Color,
                    $"/Services/{x.Service.Id}"
                )
            ).ToImmutableList();

        Update(s => s with { Events = s.Events.ToSuccess(calendarEvents) });
    }
}