using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

/// <summary>
/// FamilyId is inbound-only — Elvanto is the authority on family groupings.
/// Family ID translation (Elvanto string → local Guid) is done in the orchestrator.
/// This descriptor uses the Elvanto family ID string as the hash key so diffs are stable.
/// </summary>
public class FamilyIdDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "FamilyId";
    // Elvanto uses a numeric family group ID; the app uses GUIDs.
    // Translation is done in the orchestrator via familyIdMap (seeded from already-linked people;
    // new Elvanto families get a fresh Guid, reused for all members within the same sync batch).
    public override SyncDirection DefaultDirection => SyncDirection.InboundOnly;

    public override string? GetFromApp(DbPerson person) => person.FamilyId.ToString();
    public override void    SetOnApp(DbPerson person, string? value) =>
        person.FamilyId = Guid.TryParse(value, out Guid g) ? g : Guid.NewGuid();

    public override string? GetFromElvanto(ElvantoPerson elv)  => elv.FamilyId;
    public override void    ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) { }
}

public class FamilyGuardianDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "FamilyGuardian";
    public override SyncDirection DefaultDirection => SyncDirection.InboundOnly;

    public override string? GetFromApp(DbPerson person) => person.FamilyGuardian.ToString();
    public override void    SetOnApp(DbPerson person, string? value) =>
        person.FamilyGuardian = bool.TryParse(value, out bool b) && b;

    public override string? GetFromElvanto(ElvantoPerson elv)
    {
        bool isGuardian = elv.FamilyRelationship is "Primary Contact" or "Spouse" or "Partner";
        return isGuardian.ToString();
    }

    public override void ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) { }
}
