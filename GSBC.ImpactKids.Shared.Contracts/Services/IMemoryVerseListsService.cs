using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.MemoryVerseLists")]
public interface IMemoryVerseListsService
{
    Task<BasicResponse?> Create(
        CreateMemoryVerseListRequest request,
        CallContext             context = default
    );
    
    Task<BasicReadMultipleResponse<MemoryVerseList>?> ReadMultiple(
        MemoryVerseListsRequest request,
        CallContext             context = default
    );
    
    Task<BasicResponse?> Update(
        UpdateMemoryVerseListRequest request,
        CallContext                  context = default
    );

    Task<BasicResponse?> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}