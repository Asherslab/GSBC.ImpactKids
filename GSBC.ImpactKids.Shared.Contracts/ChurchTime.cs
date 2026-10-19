namespace GSBC.ImpactKids.Shared.Contracts;

/// <summary>
/// The church's wall clock. Instants are stored and sent in UTC everywhere; this is only for turning a
/// wall-clock idea into an instant and back - a schedule's "18:30", or which day "today" is.
/// <para>
/// <b>AEST, Queensland: no daylight saving.</b> Read by the server, the phone and the ProPresenter
/// agent alike, so the three always agree about when 18:30 is. Not the device's own zone: a phone
/// set to Sydney in summer would otherwise put every fixed time an hour out.
/// </para>
/// <para>
/// The Elvanto sync's date conversions still use Australia/Sydney (DateOfBirthDescriptor,
/// FirstTimeDescriptor, PersonMatcher, CreatePerson) - deliberately left alone here, since moving
/// them changes how dates already exchanged with Elvanto are read.
/// </para>
/// </summary>
public static class ChurchTime
{
    public const string ZoneId = "Australia/Brisbane";

    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(ZoneId);

    /// <summary>A wall-clock time at the church as UTC.</summary>
    public static DateTime ToUtc(DateTime churchTime) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(churchTime, DateTimeKind.Unspecified), Zone);

    /// <summary>An instant as the church's wall clock shows it.</summary>
    public static DateTime FromUtc(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone).DateTime;

    /// <summary>Today, at the church.</summary>
    public static DateTime Today => FromUtc(DateTimeOffset.UtcNow).Date;

    /// <summary>
    /// A church day as UTC bounds, start inclusive and end exclusive, at offset zero - which is the
    /// only offset Npgsql will write to a <c>timestamptz</c> in a query.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End) DayBounds(DateTime churchDate)
    {
        DateTimeOffset start = new DateTimeOffset(churchDate.Date, Zone.GetUtcOffset(churchDate.Date)).ToUniversalTime();
        return (start, start.AddDays(1));
    }
}
