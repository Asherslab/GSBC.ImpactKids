using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;

public partial class SyncService
{
    public async Task<BasicResponse> ApproveReview(
        ManualReviewActionRequest request,
        CallContext               context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbSyncPendingReview? review = await db.PendingReviews
            .FirstOrDefaultAsync(r => r.Id == request.Id, token);

        if (review is null)
            return BasicResponse.WithError("Review not found");

        review.Status = ManualReviewStatus.Approved;
        review.ReviewedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(token);
        await manualReviewEntryEventService.SendUpdatedEvent(token);

        return new BasicResponse { Success = true };
    }

    public async Task<BasicResponse> DenyReview(
        ManualReviewActionRequest request,
        CallContext               context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbSyncPendingReview? review = await db.PendingReviews
            .FirstOrDefaultAsync(r => r.Id == request.Id, token);

        if (review is null)
            return BasicResponse.WithError("Review not found");

        review.Status = ManualReviewStatus.Denied;
        review.ReviewedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(token);
        await manualReviewEntryEventService.SendUpdatedEvent(token);

        return new BasicResponse { Success = true };
    }
}