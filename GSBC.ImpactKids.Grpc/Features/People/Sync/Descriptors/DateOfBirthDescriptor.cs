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

    public override void SetOnApp(DbPerson person, string? value)
    {
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d))
            person.DateOfBirth = new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc));
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
