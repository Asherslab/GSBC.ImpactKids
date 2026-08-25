using System.Globalization;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

public class FirstTimeDescriptor : BaseFieldSyncDescriptor
{
    private static readonly TimeZoneInfo AestZone =
        TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    public override string        EntityType       => "Person";
    public override string        FieldName        => "FirstTime";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person) =>
        person.FirstTime.HasValue
            ? TimeZoneInfo.ConvertTime(person.FirstTime.Value, AestZone)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

    public override void SetOnApp(DbPerson person, string? value)
    {
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d))
            person.FirstTime = new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc));
    }

    public override string? GetFromElvanto(ElvantoPerson elv) =>
        string.IsNullOrWhiteSpace(elv.FirstTimeAtImpactKids) ? null : elv.FirstTimeAtImpactKids;

    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) =>
        Set(value, v => req.FirstTimeAtImpactKids = v);
}
