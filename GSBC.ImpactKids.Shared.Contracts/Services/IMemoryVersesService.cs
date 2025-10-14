using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.MemoryVerses")]
public interface IMemoryVersesService
{
    Task<BasicReadMultipleResponse<MemoryVerse>?> ReadMultiple(
        MemoryVersesRequest request,
        CallContext         context = default
    );
}