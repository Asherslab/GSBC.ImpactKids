using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

/// <summary>
/// Family membership, in both directions, decided by last-write-wins like any other field.
///
/// This was inbound-only on the grounds that Elvanto owns family groupings. That held only while
/// "when did Elvanto change?" was unanswerable: with no real Elvanto timestamp, an outbound family
/// move would have won every comparison and could regroup a household nobody touched. Elvanto's
/// date_modified closes that, so a move made in the app can now be pushed when it is genuinely the
/// newer edit, and a move made in Elvanto still wins when it is.
///
/// The two sides speak different languages - Elvanto a numeric family id, the app a Guid - so the
/// translation both ways needs the orchestrator's map. Inbound is translated before SetOnApp;
/// outbound uses <see cref="ElvantoFamilyIdByLocal"/>, primed per run.
/// </summary>
public class FamilyIdDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "FamilyId";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    /// <summary>
    /// Local family Guid → Elvanto's numeric family id, for the families this run knows about.
    /// Primed by the orchestrator, which is the only thing that can see the linked members a
    /// family's Elvanto id is derived from.
    /// </summary>
    public IReadOnlyDictionary<Guid, string>? ElvantoFamilyIdByLocal { get; set; }

    public override string? GetFromApp(DbPerson person) => person.FamilyId.ToString();
    public override void    SetOnApp(DbPerson person, string? value) =>
        person.FamilyId = Guid.TryParse(value, out Guid g) ? g : Guid.NewGuid();

    public override string? GetFromElvanto(ElvantoPerson elv)  => elv.FamilyId;

    /// <summary>
    /// Deliberately empty. Family is the one field whose outbound value depends on who is being
    /// resolved - a descriptor instance is shared across every person in the run, so it cannot
    /// answer "which Elvanto family is this person's local family?" without being told. The
    /// orchestrator sets family_id directly; see ElvantoPersonSyncService.
    /// </summary>
    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) => false;
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

    /// <summary>
    /// Nothing to push: Elvanto derives guardianship from family_relationship, which this app has no
    /// mechanism for. Says false rather than quietly no-opping, so a base can never advance on it.
    /// </summary>
    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) => false;
}
