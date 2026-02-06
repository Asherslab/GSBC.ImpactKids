using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture.Memorisation;

[Service("GSBC.ImpactKids.MemoryVerseLists")]
public interface IMemoryVerseListsService
    : IBasicReadMultipleService<MemoryVerseList>,
        ICreateService<CreateMemoryVerseListRequest>,
        IUpdateService<UpdateMemoryVerseListRequest>,
        IBasicDeleteService<MemoryVerseList>
{
    Task<BasicReadResponse<MemoryVerseList>> Read(
        BasicReadRequest request,
        CallContext      context = default
    );
}