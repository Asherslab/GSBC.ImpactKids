using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

public class PhoneNumberDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "PhoneNumber";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person) => Normalise(person.PhoneNumber);

    public override void SetOnApp(DbPerson person, string? value) =>
        person.PhoneNumber = string.IsNullOrWhiteSpace(value) ? null : value;

    public override string? GetFromElvanto(ElvantoPerson elv) =>
        Normalise(string.IsNullOrWhiteSpace(elv.Mobile)
            ? (string.IsNullOrWhiteSpace(elv.Phone) ? null : elv.Phone)
            : elv.Mobile);

    /// <summary>
    /// Strips the spacing the app's own form adds so the two sides are comparable and so a push
    /// carries digits rather than presentation. Elvanto stores "0435862120"; typing the same
    /// number into the app yields "0435 862 120", and without this the hashes differ forever -
    /// every sync sees a change, pushes it, and rewrites Elvanto's formatting for nothing.
    /// A leading "+" is kept because it carries meaning; every other non-digit does not.
    /// </summary>
    private static string? Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        string trimmed = value.Trim();
        bool   plus    = trimmed.StartsWith('+');
        string digits  = new(trimmed.Where(char.IsDigit).ToArray());

        return digits.Length == 0 ? null : plus ? $"+{digits}" : digits;
    }

    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) => Set(value, v => req.Mobile = v);
}
