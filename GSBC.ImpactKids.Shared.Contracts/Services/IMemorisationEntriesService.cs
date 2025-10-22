using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemorisationEntries;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.MemorisationEntries")]
public interface IMemorisationEntriesService
{
    Task<BasicReadMultipleResponse<MemorisationEntry>?> ReadMultiple(
        MemorisationEntriesRequest request,
        CallContext                context = default
    );

    Task<BasicResponse?> Update(
        UpdateMemorisationEntryRequest request,
        CallContext                    context = default
    );
}