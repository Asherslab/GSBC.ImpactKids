namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules;

public static class ScheduleFormat
{
    public static string Minutes(int minutes) =>
        Math.Abs(minutes) < 60
            ? $"{minutes} min"
            : minutes % 60 == 0
                ? $"{minutes / 60} h"
                : $"{minutes / 60} h {Math.Abs(minutes % 60)} min";

    /// <summary>A local time of day as the night is talked about: 18:30.</summary>
    public static string Clock(TimeSpan time) => $"{(int)time.TotalHours % 24:00}:{time.Minutes:00}";

    public static string Clock(DateTime local) => local.ToString("HH:mm");
}
