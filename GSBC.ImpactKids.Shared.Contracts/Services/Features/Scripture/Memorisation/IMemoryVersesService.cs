using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture.Memorisation;

[Service("GSBC.ImpactKids.MemoryVerses")]
public interface IMemoryVersesService
{
    Task<BasicResponse?> Create(
        CreateMemoryVerseRequest request,
        CallContext                  context = default
    );

    Task<BasicReadResponse<MemoryVerse>?> Read(
        BasicReadRequest request,
        CallContext      context = default
    );
    
    Task<BasicReadMultipleResponse<MemoryVerse>?> ReadMultiple(
        MemoryVersesRequest request,
        CallContext         context = default
    );
    
    Task<BasicResponse?> Update(
        UpdateMemoryVerseRequest request,
        CallContext                  context = default
    );

    Task<BasicResponse?> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}