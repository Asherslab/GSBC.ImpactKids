using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

/// <summary>
/// Syncs school grade as its local Guid (app side) ↔ Elvanto's school grade ID string.
/// Requires the caller to pass a school-grade lookup table via context; this descriptor
/// stores/retrieves the *app GUID* as a string so the standard hash-diff logic works.
/// Mapping from ElvantoId → local Guid is performed in the orchestrator before calling SetOnApp,
/// and the reverse - local Guid → ElvantoId - before the outbound value reaches
/// <see cref="ApplyToElvantoRequest"/>.
/// </summary>
public class SchoolGradeDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "SchoolGradeId";

    /// <summary>
    /// Bidirectional. This was InboundOnly for two separate reasons, and both are gone.
    ///
    /// The first was that the payload had no <c>school_grade</c> at all, so declaring Bidirectional
    /// made a grade change take the outbound branch, count towards OutboundFields and write a "would
    /// push" audit row naming the <i>local</i> Guid - a row a reviewer would read as "this will reach
    /// Elvanto" when the request body never carried it. The payload carries it now, in Elvanto's
    /// terms, and the orchestrator translates the app's Guid before it ever reaches the wire.
    ///
    /// The second was Elvanto's yearly grade rollover: every child's grade moves at once, and the
    /// worry was that the app would push last year's grade back over it. That worry belonged to an
    /// engine that could not tell "the app changed" from "the app has a value". This one can. A
    /// rollover moves Elvanto's leg alone, which is <c>ElvantoChangedAlone</c> and applies inbound
    /// without consulting a clock at all; the app's grade only ever leaves here when the app's leg
    /// moved and Elvanto's did not, or when a genuine two-sided conflict is broken on
    /// <c>date_modified</c> - where <see cref="PrecedenceOnTie"/> still leaves the tie to Elvanto.
    /// </summary>
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person) => person.SchoolGradeId?.ToString();

    /// <summary>
    /// A value that is not a local grade Guid is not a grade, and clearing the child's year level on
    /// the strength of it is the F9 shape - absence read as an instruction. The orchestrator already
    /// reports an Elvanto grade it cannot map as unreadable rather than as a value; this refuses the
    /// rest, and says so rather than reporting a write it did not make.
    /// </summary>
    public override bool SetOnApp(DbPerson person, string? value)
    {
        if (!Guid.TryParse(value, out Guid g)) return false;

        person.SchoolGradeId = g;
        return true;
    }

    // Returns the Elvanto school grade ID string for hash comparison
    public override string? GetFromElvanto(ElvantoPerson elv) => elv.SchoolGrade?.Id;

    /// <summary>
    /// The value reaching here is already Elvanto's grade id, not the local Guid: the comparison
    /// happens in the app's terms and the orchestrator translates the outbound leg back, the same
    /// asymmetry family has, and for the same reason - a descriptor instance is shared across
    /// everyone in the run and the grade table is not its to hold.
    ///
    /// <b>Null is declined rather than sent as a clear.</b> Two different things arrive as null - a
    /// child with no grade, and a local grade row with no <c>ElvantoId</c> - and neither is an
    /// instruction to empty a grade Elvanto is maintaining. Declining reports them as
    /// <c>NotCarried:</c> rows a person can act on, which is the same choice family makes about
    /// <c>Guid.Empty</c>.
    ///
    /// There is also no clear to send. Verified against the live API on 2026-08-27: an explicit null
    /// answers ok and changes nothing, an empty string and <c>"0"</c> both answer 500, and every
    /// spelling of "none" is rejected as an invalid value. <b>A school grade cannot be emptied
    /// through the API at all</b> - only in Elvanto's own UI. So the general rule that an empty
    /// string is a deliberate clear does not hold for this field, and must not be extended to it.
    /// </summary>
    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        req.SchoolGrade = value;
        return true;
    }
}
