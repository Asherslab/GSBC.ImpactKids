using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    private async Task<DbSyncMetadata> UpsertMetadata(
        DbPerson                           person,
        string                             elvantoId,
        int                                confidence,
        string                             strategy,
        Dictionary<string, DbSyncMetadata> metaByElvantoId,
        CancellationToken                  token = default
    )
    {
        if (metaByElvantoId.TryGetValue(elvantoId, out DbSyncMetadata? existing))
        {
            existing.MatchConfidence = confidence;
            existing.MatchStrategy = strategy;
            return existing;
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
        metaByElvantoId[elvantoId] = meta;
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
