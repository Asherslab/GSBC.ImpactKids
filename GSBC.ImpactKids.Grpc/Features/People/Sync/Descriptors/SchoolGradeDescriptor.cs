using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

/// <summary>
/// Syncs school grade as its local Guid (app side) ↔ Elvanto's school grade ID string.
/// Requires the caller to pass a school-grade lookup table via context; this descriptor
/// stores/retrieves the *app GUID* as a string so the standard hash-diff logic works.
/// Mapping from ElvantoId → local Guid is performed in the orchestrator before calling SetOnApp.
/// </summary>
public class SchoolGradeDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "SchoolGradeId";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person) => person.SchoolGradeId?.ToString();

    public override void SetOnApp(DbPerson person, string? value) =>
        person.SchoolGradeId = Guid.TryParse(value, out Guid g) ? g : null;

    // Returns the Elvanto school grade ID string for hash comparison
    public override string? GetFromElvanto(ElvantoPerson elv) => elv.SchoolGrade?.Id;

    // School grade inbound is handled via ElvantoId → local Guid lookup in orchestrator;
    // outbound is not supported (Elvanto school grade IDs are managed by Elvanto).
    public override void ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) { }
}
