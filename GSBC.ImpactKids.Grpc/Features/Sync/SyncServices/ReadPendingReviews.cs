using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;

public partial class SyncService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<SyncManualReviewEntry>> ReadPendingReviews(
        CallContext context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbSyncPendingReview> query = db.PendingReviews
            .OrderByDescending(x => x.CreatedAt);

        await foreach (BasicReadMultipleResponse<SyncManualReviewEntry> response in
            query.ReturnInBatches(pendingReviewConverter, token: token))
        {
            yield return response;
        }
    }
}
