using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Analyitcs;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemorisationEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Analytics;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.MemorisationEntries")]
public interface IMemorisationEntriesService
{
    public Task<MemoryVerseAnalyticsResponse?> RetrieveAnalyticsData(
        MemorisationEntriesAnalyticsRequest request,
        CallContext                         context = default
    );
    
    Task<BasicReadMultipleResponse<MemorisationEntry>?> ReadMultiple(
        MemorisationEntriesRequest request,
        CallContext                context = default
    );

    Task<BasicResponse?> Update(
        UpdateMemorisationEntryRequest request,
        CallContext                    context = default
    );
}