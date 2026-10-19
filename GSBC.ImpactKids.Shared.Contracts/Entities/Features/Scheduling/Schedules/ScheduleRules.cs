namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

/// <summary>What makes a schedule item's time valid. Read by the phone before sending and by the server before saving.</summary>
public static class ScheduleRules
{
    public const int MaxMinutes = 600;

    public static bool MinutesValid(int minutes) => minutes is >= 0 and <= MaxMinutes;

    public static bool StartsAtValid(TimeSpan? startsAt) =>
        startsAt == null || (startsAt.Value >= TimeSpan.Zero && startsAt.Value < TimeSpan.FromDays(1));
}
