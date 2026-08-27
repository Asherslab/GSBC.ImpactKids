using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

/// <summary>
/// One run's view of <see cref="DbElvantoFamilyLink"/>: which local family each Elvanto household
/// is, and the other way round.
///
/// This replaces an inference. The pairing used to be re-derived every run from the fetched roll,
/// which made it two different kinds of wrong at once — a household whose only fetched member was
/// the person being asked about had no evidence and diverged forever, while a household that did
/// have evidence answered from whoever the run happened to see, so the map moved when the roll did.
/// A remembered pairing does neither: it is the same answer this run and every run after, and a
/// local move by the asker no longer changes what the asker's household is said to be, which is
/// what makes the move visible rather than self-confirming.
///
/// Rows added during a run are handed back to the caller to persist — this type does not touch the
/// database, so a run that fails leaves the table as it found it.
/// </summary>
public sealed class SyncFamilyLinks
{
    /// <summary>
    /// The app's "no family yet" bucket: 412 unrelated people sharing one Guid, a data problem that
    /// predates the sync engine entirely.
    ///
    /// <b>Nothing may map it.</b> Twelve of those people carry a snapshot naming a single Elvanto
    /// household, and pairing the bucket with it would declare 412 strangers one family and start
    /// syncing them as one. Splitting it needs a human who knows the families; until then a bucketed
    /// person whose Elvanto household is unknown here is reported as unmappable rather than guessed
    /// at or quietly moved.
    /// </summary>
    public static readonly Guid UngroupedBucket = new("b1680e5d-01cc-4472-9e71-5df136814247");

    private readonly Dictionary<string, Guid> _localByElvanto;
    private readonly Dictionary<Guid, string> _elvantoByLocal;
    private readonly List<DbElvantoFamilyLink> _added = [];
    private readonly Dictionary<string, string> _unmappable = [];

    public SyncFamilyLinks(IEnumerable<DbElvantoFamilyLink> rows)
    {
        List<DbElvantoFamilyLink> all = rows.ToList();
        _localByElvanto = all.ToDictionary(x => x.ElvantoFamilyId, x => x.LocalFamilyId);
        _elvantoByLocal = all.ToDictionary(x => x.LocalFamilyId, x => x.ElvantoFamilyId);
    }

    /// <summary>
    /// Whether a local family Guid is one this app is willing to pair with an Elvanto household.
    /// <see cref="Guid.Empty"/> is the column default — "no family recorded" — and the bucket is a
    /// known lie; neither is a household.
    /// </summary>
    public static bool IsMappable(Guid localFamilyId) =>
        localFamilyId != Guid.Empty && localFamilyId != UngroupedBucket;

    /// <summary>The local family an Elvanto household is, or null when this app has never been told.</summary>
    public Guid? LocalFor(string elvantoFamilyId) =>
        _localByElvanto.TryGetValue(elvantoFamilyId, out Guid local) ? local : null;

    /// <summary>The Elvanto household a local family is, or null when it has no counterpart yet.</summary>
    public string? ElvantoFor(Guid localFamilyId) =>
        _elvantoByLocal.TryGetValue(localFamilyId, out string? elvanto) ? elvanto : null;

    /// <summary>Rows this run created, for the caller to persist alongside its other writes.</summary>
    public IReadOnlyList<DbElvantoFamilyLink> Added => _added;

    /// <summary>How many pairings are known, stored and learned together.</summary>
    public int StoredCount => _localByElvanto.Count;

    /// <summary>
    /// Households that must not be paired, and why — the text that becomes the audit row's reason.
    ///
    /// A household two local families claim is a merge and a local family spread across two
    /// households is a split, and both change who is related to whom. Left unmarked they would be
    /// settled by whichever member the loop happened to reach first, which is the order-dependent
    /// guessing this table exists to end; marked, they are a question with a household number in it.
    /// </summary>
    public void MarkUnmappable(string elvantoFamilyId, string reason) =>
        _unmappable[elvantoFamilyId] = reason;

    /// <inheritdoc cref="MarkUnmappable"/>
    public string? UnmappableReason(string elvantoFamilyId) =>
        _unmappable.GetValueOrDefault(elvantoFamilyId);

    /// <summary>
    /// Records a pairing, and returns the row to persist — or null when either side is already
    /// spoken for, because both sides are unique and an existing row is the answer.
    /// </summary>
    public DbElvantoFamilyLink? Record(Guid localFamilyId, string elvantoFamilyId, ElvantoFamilyLinkSource source)
    {
        if (string.IsNullOrWhiteSpace(elvantoFamilyId) || !IsMappable(localFamilyId)) return null;
        if (_localByElvanto.ContainsKey(elvantoFamilyId) || _elvantoByLocal.ContainsKey(localFamilyId)) return null;

        DbElvantoFamilyLink row = new()
        {
            Id              = Guid.NewGuid(),
            LocalFamilyId   = localFamilyId,
            ElvantoFamilyId = elvantoFamilyId,
            Source          = source,
            LinkedAtUtc     = DateTimeOffset.UtcNow
        };

        _localByElvanto[elvantoFamilyId] = localFamilyId;
        _elvantoByLocal[localFamilyId]   = elvantoFamilyId;
        _added.Add(row);
        return row;
    }
}
