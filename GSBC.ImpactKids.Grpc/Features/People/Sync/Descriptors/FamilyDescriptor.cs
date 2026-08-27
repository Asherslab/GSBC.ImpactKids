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
/// translation both ways needs the persisted pairing in <c>ElvantoFamilyLinks</c>. A descriptor
/// instance is shared across every person in the run and cannot reach the database, so the
/// orchestrator does the translation in both directions; see ElvantoPersonSyncService.
/// </summary>
public class FamilyIdDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "FamilyId";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person) => person.FamilyId.ToString();

    /// <summary>
    /// Only ever assigns a family it was actually given. This used to fall back to
    /// <c>Guid.NewGuid()</c>, so a value it could not read moved the person into a brand-new
    /// one-person household — and because the field then had no Elvanto value to record, the
    /// snapshot never advanced and it happened again on every run. Minting a local family is now
    /// the orchestrator's job and happens at most once per Elvanto household, ever, against a row
    /// in <c>ElvantoFamilyLinks</c>; this refusal is the backstop that keeps it that way.
    /// </summary>
    public override bool SetOnApp(DbPerson person, string? value)
    {
        if (!Guid.TryParse(value, out Guid g)) return false;

        person.FamilyId = g;
        return true;
    }

    // Deliberately NOT overriding IsValidInboundValue. By the time a value reaches this hook the
    // orchestrator has already turned Elvanto's household into a local family Guid, so "is this
    // usable?" has been answered upstream: a household this app cannot place is reported as unknown
    // rather than handed over as a value, and one it can place is a Guid SetOnApp will take. The
    // base's "blank is not a value" is not a change here either - "no household" arrives as
    // Guid.Empty spelled out, which is a value, and is meant to be applied.

    /// <summary>
    /// A blank <c>family_id</c> is not a family. Elvanto returns one for people it has no household
    /// for, and carrying it through as a value made an empty string the answer to "which Elvanto
    /// family is this person's local family?" — which then read as a deliberate clear and planned to
    /// empty the family of everyone whose relatives happened to have no Elvanto household.
    /// </summary>
    public override string? GetFromElvanto(ElvantoPerson elv) =>
        string.IsNullOrWhiteSpace(elv.FamilyId) ? null : elv.FamilyId;

    /// <summary>
    /// Deliberately empty. Family is the one field whose outbound value depends on who is being
    /// resolved - a descriptor instance is shared across every person in the run, so it cannot
    /// answer "which Elvanto family is this person's local family?" without being told. The
    /// orchestrator sets family_id directly; see ElvantoPersonSyncService.
    /// </summary>
    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) => false;
}

/// <summary>
/// Guardianship: one app boolean against Elvanto's eight family relationships, so the two sides
/// meet on a derived value rather than on the relationship itself.
///
/// Inbound accepts any of Elvanto's guardian roles; outbound only ever writes "Primary Contact".
/// This was inbound-only, which made a ticked box a permanent divergence - the direction refused
/// the push forever, so the base could never advance and every run reported the same
/// DirectionRefused row again. The promote half has a single correct answer and is now pushed; the
/// demote half has none and is refused explicitly, which is a different and honest kind of stuck.
/// </summary>
public class FamilyGuardianDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "FamilyGuardian";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person) => person.FamilyGuardian.ToString();
    public override bool    SetOnApp(DbPerson person, string? value) =>
        Assign(value, v => person.FamilyGuardian = bool.TryParse(v, out bool b) && b);

    /// <summary>
    /// Collapses every guardian role Elvanto has to one boolean, and that collapse is what makes
    /// "never override someone who is already a guardian in another form" free. Do NOT add a check
    /// for it here or in <see cref="ApplyToElvantoRequest"/>: a Spouse or Partner already derives
    /// "True", so an app that also says guardian hashes equal on both sides and FieldReconciler
    /// returns Agreed before direction is ever consulted - no push is planned, and the base settles.
    /// An outbound can therefore only ever fire with app="True" against a derived "False", which is
    /// Child, Other, or no relationship at all. That is exactly the promote case, so a guard would
    /// be dead code that reads as though it were load-bearing.
    /// </summary>
    public override string? GetFromElvanto(ElvantoPerson elv)
    {
        bool isGuardian = elv.FamilyRelationship is "Primary Contact" or "Spouse" or "Partner";
        return isGuardian.ToString();
    }

    /// <summary>
    /// Promotes, and refuses to demote.
    ///
    /// "True" writes Primary Contact. Of Elvanto's guardian roles the app cannot say which one a
    /// person holds, and Primary Contact is the one that is always defensible - and by the reasoning
    /// on <see cref="GetFromElvanto"/> this is only ever reached for someone Elvanto does not
    /// currently treat as a guardian at all.
    ///
    /// "False" returns false, and that is deliberate rather than an oversight. App-says-not-a-guardian
    /// has no safe target: Child is wrong for an adult, and Other is what Elvanto uses for "no
    /// household", so either guess restructures a household on the strength of an unticked box. The
    /// refusal makes WouldCarry fail, so the engine records NotCarried:AppChangedAlone as a
    /// divergence - a human sees it and fixes the relationship in Elvanto, where the eight-way choice
    /// actually lives. Returning false also keeps the base from advancing, so the disagreement is
    /// reported every run until someone settles it, which is the wanted behaviour and not a gap.
    /// </summary>
    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value)
    {
        if (!bool.TryParse(value, out bool isGuardian) || !isGuardian) return false;

        req.FamilyRelationship = "Primary Contact";
        return true;
    }
}
