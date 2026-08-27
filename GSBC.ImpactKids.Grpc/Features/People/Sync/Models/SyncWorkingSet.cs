using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

/// <summary>
/// Everything one run reads before it decides anything, loaded once and named.
///
/// Both phases need the same picture — Decide to reach its decisions, Apply to check that the
/// picture has not moved since — so building it is one method rather than two that can drift.
/// </summary>
public sealed class SyncWorkingSet
{
    public required List<ElvantoPerson>       ElvantoPeople { get; init; }

    /// <summary>
    /// Whether this run is allowed to bring new people into the app.
    ///
    /// <b>False for every scoped run.</b> A scoped fetch is not a scoped roll: asking Elvanto about
    /// one person or one family pulls the <i>whole</i> roll the moment any member is unlinked, so the
    /// matcher can find them — while the app side loads only that person or family. Nothing then
    /// distinguishes "an Elvanto person this run is about" from "the other seventeen hundred", and
    /// every one of them became a person to create. A `Scope=Person` dry run planned ~1718 creates.
    /// </summary>
    public required bool MayCreateLocalPeople { get; init; }
    public required List<DbPerson>            AppPeople     { get; init; }
    public required List<DbSchoolGrade>       SchoolGrades  { get; init; }

    public required Dictionary<(Guid, string), DbElvantoFieldSnapshot> Bases          { get; init; }
    public required Dictionary<(Guid, string), DateTimeOffset>         LastAppChange  { get; init; }
    public required Dictionary<(Guid, string), DbSyncPendingReview>    PendingReviews { get; init; }

    public required Dictionary<string, DbPerson> AppByElvantoId { get; init; }
    public required List<DbPerson>               UnlinkedApp    { get; init; }

    /// <summary>
    /// The persisted local-family ⟷ Elvanto-household pairing, and the only thing this run
    /// consults about family.
    ///
    /// It used to be three dictionaries derived from the fetched roll, and the derivation was the
    /// bug. "Which local family is Elvanto household 42?" was answered only from <i>other</i> app
    /// people already known to be in it, and the roll excludes contacts — so a household whose only
    /// non-contact member was the person being asked about had no evidence at all and diverged on
    /// every run, forever. Excluding the asker was nonetheless right: while the map was rebuilt per
    /// run, including them made every answer self-confirming and fourteen people ping-ponged
    /// between families on a real run. Remembering the pairing removes the need for either — a
    /// stored row does not move when the roll does, so the asker cannot confirm themselves.
    /// </summary>
    public required SyncFamilyLinks Families { get; init; }
}
