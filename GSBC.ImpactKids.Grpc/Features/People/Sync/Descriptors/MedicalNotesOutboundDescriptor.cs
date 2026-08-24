using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

/// <summary>
/// Pushes App medical notes to Elvanto's medical custom field as a comma-separated string.
/// Inbound not supported for the same reason as AllergiesOutboundDescriptor.
/// </summary>
public class MedicalNotesOutboundDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "MedicalNotes";
    public override SyncDirection DefaultDirection => SyncDirection.OutboundOnly;

    public override string? GetFromApp(DbPerson person)
    {
        if (person.MedicalNotes.Count == 0) return null;
        return string.Join(", ", person.MedicalNotes
            .Where(n => !string.IsNullOrWhiteSpace(n.Notes))
            .Select(n => n.Notes));
    }

    public override void    SetOnApp(DbPerson person, string? value) { }
    public override string? GetFromElvanto(ElvantoPerson elv) => null;

    public override void ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value)
    {
        if (value is not null)
            req.MedicalAllergyNotes = MergeWithExisting(req.MedicalAllergyNotes, value, "Medical");
    }

    private static string MergeWithExisting(string? existing, string newSection, string label) =>
        string.IsNullOrWhiteSpace(existing) ? $"{label}: {newSection}" : $"{existing}\n{label}: {newSection}";
}
