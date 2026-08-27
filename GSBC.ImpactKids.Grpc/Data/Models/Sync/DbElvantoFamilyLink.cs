using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Data.Models.Sync;

/// <summary>
/// Which Elvanto household a local family <b>is</b> — remembered, rather than re-derived from
/// whoever else happened to be in the fetched roll.
///
/// A local family is a bare <c>uuid</c> column on <c>People</c>; there is no Families table and
/// never has been, so this is the only place the pairing can live. Without it the run answered
/// "which local family is Elvanto household 42?" purely from <i>other</i> app people already known
/// to be in it — and the roll excludes contacts, so a household whose only non-contact member is
/// the person being asked about had no evidence at all and diverged on every run, forever. 97 of
/// them on a real run.
///
/// Both sides are unique: one local family is one Elvanto household. That is what makes the row an
/// answer rather than a hint, and it is what stops the minting this replaces — that minting was per
/// person, per run, with no memory, so it re-fired forever and scattered 411 people into
/// one-person households.
/// </summary>
public class DbElvantoFamilyLink
{
    public required Guid   Id              { get; set; }
    public required Guid   LocalFamilyId   { get; set; }
    public required string ElvantoFamilyId { get; set; }

    public required ElvantoFamilyLinkSource Source     { get; set; }
    public required DateTimeOffset          LinkedAtUtc { get; set; }
}
