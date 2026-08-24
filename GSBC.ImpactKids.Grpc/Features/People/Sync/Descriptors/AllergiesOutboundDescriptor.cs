using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

/// <summary>
/// Pushes App allergies to Elvanto's medical custom field as a comma-separated string.
/// Inbound is not supported — Elvanto's single free-text field can't be parsed back
/// into the App's structured allergy model.
/// </summary>
public class AllergiesOutboundDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "Allergies";
    public override SyncDirection DefaultDirection => SyncDirection.OutboundOnly;

    public override string? GetFromApp(DbPerson person)
    {
        if (person.Allergies.Count == 0) return null;
        return string.Join(", ", person.Allergies
            .Where(a => !string.IsNullOrWhiteSpace(a.Notes))
            .Select(a => a.Notes));
    }

    public override void    SetOnApp(DbPerson person, string? value) { }
    public override string? GetFromElvanto(ElvantoPerson elv) => null;

    public override void ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value)
    {
        if (value is not null)
            req.MedicalAllergyNotes = MergeWithExisting(req.MedicalAllergyNotes, value, "Allergies");
    }

    private static string MergeWithExisting(string? existing, string newSection, string label) =>
        string.IsNullOrWhiteSpace(existing) ? $"{label}: {newSection}" : $"{existing}\n{label}: {newSection}";
}
