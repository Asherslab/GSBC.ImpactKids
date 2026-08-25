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
    public required List<DbPerson>            AppPeople     { get; init; }
    public required List<DbSchoolGrade>       SchoolGrades  { get; init; }
    public required SyncMetadataIndex         Metadata      { get; init; }

    public required Dictionary<string, DbSyncFieldConfig>              FieldConfigs   { get; init; }
    public required Dictionary<(Guid, string), DbElvantoFieldSnapshot> Bases          { get; init; }
    public required Dictionary<(Guid, string), DateTimeOffset>         LastAppChange  { get; init; }
    public required Dictionary<(Guid, string), DbSyncPendingReview>    PendingReviews { get; init; }

    public required Dictionary<string, DbPerson> AppByElvantoId { get; init; }
    public required List<DbPerson>               UnlinkedApp    { get; init; }

    /// <summary>Elvanto family id → the local family Guid it corresponds to.</summary>
    public required Dictionary<string, Guid> FamilyIdMap { get; init; }

    /// <summary>Local family Guid → the Elvanto family its members agree on.</summary>
    public required Dictionary<Guid, string> ElvantoFamilyIdByLocal { get; init; }

    /// <summary>
    /// Where each local family's linked members actually sit in Elvanto. Kept as the raw membership
    /// rather than a single answer per family, because who is asking matters — see
    /// <see cref="ResolveFamilyInElvanto"/>.
    /// </summary>
    public required Dictionary<Guid, List<(Guid PersonId, string ElvantoFamilyId)>> FamilyMembership { get; init; }

    /// <summary>
    /// The Elvanto family a local family corresponds to, as evidenced by its members <b>other than
    /// the one asking</b>.
    ///
    /// Excluding the asker is the whole point: a person is the only evidence for their own family
    /// when they are its sole member, so including them makes any answer self-confirming — a person
    /// moved into a brand new local family would be read as that family's Elvanto pairing and
    /// compare equal to itself, and the move would never be seen. Excluding them, a lone mover has
    /// no evidence, which is exactly right: their new family has no Elvanto counterpart and one has
    /// to be created.
    /// </summary>
    public string? ResolveFamilyInElvanto(Guid localFamilyId, Guid askingPersonId) =>
        FamilyMembership.TryGetValue(localFamilyId, out List<(Guid PersonId, string ElvantoFamilyId)>? members)
            ? members.Where(m => m.PersonId != askingPersonId)
                .GroupBy(m => m.ElvantoFamilyId)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .FirstOrDefault()?.Key
            : null;
}
