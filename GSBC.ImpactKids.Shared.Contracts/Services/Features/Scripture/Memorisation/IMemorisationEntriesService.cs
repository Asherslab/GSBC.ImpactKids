using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemorisationEntries;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture.Memorisation;

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