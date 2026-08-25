using GSBC.ImpactKids.Grpc.Data.Models.Sync;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

/// <summary>
/// The run's view of <c>SyncMetadata</c>, indexed by <b>both</b> of the columns the table is unique
/// on. Holding only the <c>ElvantoId</c> map is what made an entire run fail: the low-confidence
/// path writes a row for an unlinked person against the Elvanto id it was compared to, and when
/// that person later linked to their own Elvanto id no row was found under it, so a fresh row was
/// added carrying a <c>PersonId</c> the unique index already held.
///
/// Every mutation goes through <see cref="Upsert"/> so the two maps cannot drift apart.
/// </summary>
public sealed class SyncMetadataIndex
{
    private readonly Dictionary<string, DbSyncMetadata> _byElvantoId;
    private readonly Dictionary<Guid, DbSyncMetadata>   _byPersonId;

    public SyncMetadataIndex(IEnumerable<DbSyncMetadata> rows)
    {
        List<DbSyncMetadata> all = rows.ToList();
        _byElvantoId = all.ToDictionary(x => x.ElvantoId);
        _byPersonId  = all.ToDictionary(x => x.PersonId);
    }

    public bool TryGetByElvantoId(string elvantoId, out DbSyncMetadata? meta) =>
        _byElvantoId.TryGetValue(elvantoId, out meta);

    public bool TryGetByPersonId(Guid personId, out DbSyncMetadata? meta) =>
        _byPersonId.TryGetValue(personId, out meta);

    /// <summary>
    /// Records a row that is already tracked by EF, keyed both ways. Re-keying an existing row's
    /// <c>ElvantoId</c> is the caller's job to do on the entity; this keeps the maps honest about it.
    /// </summary>
    public void Add(DbSyncMetadata meta, string? previousElvantoId = null)
    {
        if (previousElvantoId is not null) _byElvantoId.Remove(previousElvantoId);
        _byElvantoId[meta.ElvantoId] = meta;
        _byPersonId[meta.PersonId]   = meta;
    }
}
