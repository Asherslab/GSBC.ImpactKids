using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Sync;
using ProtoBuf.Grpc;

namespace GSBC.ImpactKids.WASM.Services;

internal sealed class SyncManualReviewReadAdapter(ISyncService syncService)
    : IBasicReadMultipleService<SyncManualReviewEntry>
{
    public IAsyncEnumerable<BasicReadMultipleResponse<SyncManualReviewEntry>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    ) => syncService.ReadPendingReviews(context);
}
