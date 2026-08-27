using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    /// <summary>
    /// Saves reviews raised this run, outside the phase's own save.
    ///
    /// A review has to outlive the run that raised it whatever else happens, because it is the only
    /// thing a person can act on - and the run that raises one is usually the run someone is
    /// reviewing. Existing pairs are skipped rather than updated: a decided review is a permanent
    /// record, and an approved duplicate is the statement that two app records are the same person.
    /// </summary>
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
