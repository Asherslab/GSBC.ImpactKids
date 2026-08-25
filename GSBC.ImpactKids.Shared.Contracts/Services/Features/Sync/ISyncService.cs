using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.People;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Sync;

[Service("gRPC/GSBC.ImpactKids.Sync")]
public interface ISyncService
    : IBasicReadMultipleService<SyncOperation>
{
    Task<SyncResponse> CreateSync(SyncWithElvantoRequest request, CallContext context = default);

    Task<BasicReadResponse<SyncOperation>> Read(BasicReadRequest request, CallContext context = default);

    IAsyncEnumerable<BasicReadMultipleResponse<SyncAuditLog>> ReadAuditLogs(
        BasicReadRequest request,
        CallContext      context = default
    );

    IAsyncEnumerable<BasicReadMultipleResponse<SyncManualReviewEntry>> ReadPendingReviews(
        CallContext context = default
    );

    IAsyncEnumerable<BasicReadMultipleResponse<SyncPlannedChange>> ReadPlannedChanges(
        BasicReadRequest request,
        CallContext      context = default
    );

    /// <summary>Executes the plan an earlier run decided. Refuses an expired plan outright.</summary>
    Task<SyncResponse> ExecutePlan(ExecutePlanRequest request, CallContext context = default);

    Task<BasicResponse> ApproveReview(ManualReviewActionRequest request, CallContext context = default);

    Task<BasicResponse> DenyReview(ManualReviewActionRequest request, CallContext context = default);
}
