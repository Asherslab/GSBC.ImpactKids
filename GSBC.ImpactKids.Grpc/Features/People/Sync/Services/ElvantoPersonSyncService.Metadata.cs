using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    /// <summary>
    /// Records the link between an app person and an Elvanto record.
    ///
    /// <c>DbSyncMetadata</c> is unique on <c>ElvantoId</c> <b>and</b> on <c>PersonId</c>
    /// (<c>GsbcDbContext.SyncModel.cs</c>), so both have to be asked before adding. Looking up by
    /// <c>ElvantoId</c> alone failed whole runs: the low-confidence path writes a row for an
    /// unlinked person against the Elvanto id it was compared to, and when that person later linked
    /// to their own Elvanto id nothing was found under it, a second row was added, and
    /// <c>SaveChanges</c> died on the <c>PersonId</c> index.
    /// </summary>
    private async Task<DbSyncMetadata> UpsertMetadata(
        DbPerson          person,
        string            elvantoId,
        int               confidence,
        string            strategy,
        SyncMetadataIndex metadata,
        CancellationToken token = default
    )
    {
        if (metadata.TryGetByElvantoId(elvantoId, out DbSyncMetadata? existing))
        {
            // Two app people claiming one Elvanto record. Re-pointing PersonId would strand the
            // other one and re-pointing ElvantoId would violate the other index, so the row is
            // left exactly as it is and the collision is named instead of being written over.
            if (existing!.PersonId != person.Id)
                logger.LogWarning(
                    "Sync: metadata for Elvanto {ElvantoId} already belongs to app person {OwnerId}; "
                    + "leaving it alone rather than re-pointing it at {PersonId}",
                    elvantoId, existing.PersonId, person.Id);
            else
            {
                existing.MatchConfidence = confidence;
                existing.MatchStrategy   = strategy;
            }

            return existing;
        }

        // This person already has a row, against a different Elvanto id - the review artefact the
        // finding describes. The link they have now is the true one, so the row moves to it rather
        // than a second row being added against the same PersonId.
        if (metadata.TryGetByPersonId(person.Id, out DbSyncMetadata? mine))
        {
            string previous = mine!.ElvantoId;
            logger.LogInformation(
                "Sync: re-pointing metadata for app person {PersonId} from Elvanto {Previous} to {ElvantoId}",
                person.Id, previous, elvantoId);

            mine.ElvantoId          = elvantoId;
            mine.MatchConfidence    = confidence;
            mine.MatchStrategy      = strategy;
            mine.MatchedAt          = DateTimeOffset.UtcNow;
            mine.LastSyncStatus     = SyncStatus.Success;
            mine.ManualReviewReason = null;

            metadata.Add(mine, previous);
            return mine;
        }

        DbSyncMetadata meta = new()
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            ElvantoId = elvantoId,
            MatchConfidence = confidence,
            MatchedAt = DateTimeOffset.UtcNow,
            MatchStrategy = strategy,
            LastSyncStatus = SyncStatus.Success
        };
        await db.SyncMetadata.AddAsync(meta, token);
        metadata.Add(meta);
        return meta;
    }

    private async Task SaveNewPendingReviewsAsync(
        List<DbSyncPendingReview> reviews,
        CancellationToken         token
    )
    {
        if (reviews.Count == 0) return;

        List<Guid> personIds = reviews.Select(r => r.PersonId).ToList();
        HashSet<(Guid, string)> existing = (await db.PendingReviews
                .Where(r => personIds.Contains(r.PersonId))
                .Select(r => new { r.PersonId, r.ElvantoId })
                .ToListAsync(token))
            .Select(r => (r.PersonId, r.ElvantoId))
            .ToHashSet();

        bool added = false;
        foreach (DbSyncPendingReview review in reviews.Where(r => !existing.Contains((r.PersonId, r.ElvantoId))))
        {
            db.PendingReviews.Add(review);
            added = true;
        }

        if (added)
            await db.SaveChangesAsync(token);
    }
}
