using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Calendar.Components;

public partial class Calendar : ComponentBase
{
    [Parameter]
    public DateTime Date { get; set; } = DateTime.Now;

    [Parameter]
    public ImmutableList<CalendarEvent>? Events { get; set; } = [];

    [Parameter]
    public ImmutableList<CalendarTerm>? Terms { get; set; } = [];

    private int                     _numberOfWeeks;
    private List<CalendarDayRecord> _calendarDays = [];

    protected override void OnParametersSet()
    {
        UpdateCalendarDays();
    }

    private record CalendarDayRecord(
        DateTime                    Date,
        bool                        OutOfMonth,
        ICollection<CalendarEvent>? Events,
        int                         Week,
        CalendarTerm?               Term
    );

    private void UpdateCalendarDays()
    {
        _calendarDays = [];

        _numberOfWeeks = 0;
        DateTime firstOfMonth = new(Date.Year, Date.Month, 1);
        CalculateOutOfMonthDaysBefore(firstOfMonth);

        int daysInMonth = DateTime.DaysInMonth(firstOfMonth.Year, firstOfMonth.Month);
        for (int i = 1; i <= daysInMonth; i++)
        {
            DateTime thisDay = new(firstOfMonth.Year, firstOfMonth.Month, i);
            if (thisDay.DayOfWeek == DayOfWeek.Sunday)
                _numberOfWeeks++;

            ICollection<CalendarEvent>? events = Events?.Where(x => thisDay.Date == x.Date.Date).ToList();
            CalendarTerm?               term = Terms?.FirstOrDefault(x => x.StartDate <= thisDay && thisDay <= x.EndDate);
            _calendarDays.Add(new CalendarDayRecord(
                thisDay,
                false,
                events,
                _numberOfWeeks,
                term
            ));
        }

        DateTime lastOfMonth = new(Date.Year, Date.Month, daysInMonth);
        CalculateOutOfMonthDaysAfter(lastOfMonth);

        StateHasChanged();
    }

    private void CalculateOutOfMonthDaysBefore(DateTime firstOfMonth)
    {
        if (firstOfMonth.DayOfWeek == DayOfWeek.Sunday) return;
        _numberOfWeeks++;

        int daysBeforeFirst = (int)firstOfMonth.DayOfWeek;

        int lastMonthYear = firstOfMonth.Year;
        int lastMonth     = firstOfMonth.Month - 1;

        if (lastMonth == 0) // december last year
        {
            lastMonthYear -= 1;
            lastMonth = 12;
        }

        int lastDayOfPreviousMonth = DateTime.DaysInMonth(lastMonthYear, lastMonth);

        for (int i = daysBeforeFirst - 1; i >= 0; i--)
        {
            DateTime                    thisDay = new(lastMonthYear, lastMonth, lastDayOfPreviousMonth - i);
            ICollection<CalendarEvent>? events = Events?.Where(x => thisDay.Date == x.Date.Date).ToList();
            CalendarTerm?               term = Terms?.FirstOrDefault(x => x.StartDate < thisDay && thisDay < x.EndDate);
            _calendarDays.Add(new CalendarDayRecord(
                thisDay,
                true,
                events,
                _numberOfWeeks,
                term
            ));
        }
    }

    private void CalculateOutOfMonthDaysAfter(DateTime lastOfMonth)
    {
        if (lastOfMonth.DayOfWeek == DayOfWeek.Saturday) return;

        // 6 because sunday is 0
        int daysAfterLast = 6 - (int)lastOfMonth.DayOfWeek;

        int nextMonthYear = lastOfMonth.Year;
        int nextMonth     = lastOfMonth.Month + 1;

        if (nextMonth == 13) // january next year
        {
            nextMonthYear += 1;
            nextMonth = 1;
        }

        for (int i = 1; i <= daysAfterLast; i++)
        {
            DateTime                    thisDay = new(nextMonthYear, nextMonth, i);
            ICollection<CalendarEvent>? events = Events?.Where(x => thisDay.Date == x.Date.Date).ToList();
            CalendarTerm?               term = Terms?.FirstOrDefault(x => x.StartDate < thisDay && thisDay < x.EndDate);
            _calendarDays.Add(new CalendarDayRecord(
                thisDay,
                true,
                events,
                _numberOfWeeks,
                term
            ));
        }
    }

    public static int NumberOfParticularDaysInMonth(int year, int month, DayOfWeek dayOfWeek)
    {
        DateTime startDate = new(year, month, 1);
        int      totalDays = startDate.AddMonths(1).Subtract(startDate).Days;

        int answer = Enumerable
            .Range(1, totalDays)
            .Select(item => new DateTime(year, month, item))
            .Count(date => date.DayOfWeek == dayOfWeek);

        return answer;
    }
}