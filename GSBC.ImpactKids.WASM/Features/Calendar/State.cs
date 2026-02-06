using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;

namespace GSBC.ImpactKids.WASM.Features.Calendar;

public record CalendarState(
    DateTime                                Date,
    AsyncData<ImmutableList<CalendarTerm>>  Terms,
    AsyncData<ImmutableList<CalendarEvent>> Events
) : IInitialisableState<CalendarState>
{
    public static CalendarState Initial => new(
        DateTime.Now,
        AsyncData<ImmutableList<CalendarTerm>>.NotAsked(),
        AsyncData<ImmutableList<CalendarEvent>>.NotAsked()
    );

    public CalendarState PreviousMonth()             => this with { Date = Date.AddMonths(-1) };
    public CalendarState NextMonth()                 => this with { Date = Date.AddMonths(1) };
    public CalendarState SetDate(DateTime? dateTime) => this with { Date = dateTime?.Date ?? DateTime.Now.Date };
}

public record CalendarTerm(
    DateTime StartDate,
    DateTime EndDate,
    string   Name,
    string?  Color
);

public record CalendarEvent(
    DateTime Date,
    string   Name,
    string?  Color,
    string?  Href
);