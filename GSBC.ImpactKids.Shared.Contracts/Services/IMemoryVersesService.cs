using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerses;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

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