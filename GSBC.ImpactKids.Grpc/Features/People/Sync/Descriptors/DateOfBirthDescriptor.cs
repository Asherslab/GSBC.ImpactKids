using System.Globalization;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

public class DateOfBirthDescriptor : BaseFieldSyncDescriptor
{
    // Elvanto stores birthdays as AEST local dates; DB stores DateOfBirth as UTC
    private static readonly TimeZoneInfo AestZone =
        TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    public override string        EntityType       => "Person";
    public override string        FieldName        => "DateOfBirth";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person) =>
        person.DateOfBirth.HasValue
            ? TimeZoneInfo.ConvertTime(person.DateOfBirth.Value, AestZone)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

    /// <summary>
    /// Only ever assigns a date it could actually read. Returning false when it could not is the
    /// point: this used to refuse silently, and the caller then recorded the refusal as a completed
    /// inbound write and settled the base on Elvanto's blank. The child kept their birthday, the
    /// base said they had none, and the next run read that gap as the app having changed and planned
    /// to push the birthday to Elvanto - a write nobody asked for.
    /// </summary>
    public override bool SetOnApp(DbPerson person, string? value)
    {
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d))
            return false;

        person.DateOfBirth = new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc));
        return true;
    }

    public override string? GetFromElvanto(ElvantoPerson elv)
    {
        if (string.IsNullOrWhiteSpace(elv.Birthday)) return null;
        // Elvanto allows year-less dates (e.g. "05-08"). Treat them as null — without a year
        // we can't calculate age, and storing a partial date would corrupt the field.
        return DateTime.TryParseExact(elv.Birthday, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? elv.Birthday
            : null;
    }

    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) => Set(value, v => req.Birthday = v);
}
