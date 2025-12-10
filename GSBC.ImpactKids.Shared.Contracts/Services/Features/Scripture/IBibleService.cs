using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture;

[Service("GSBC.ImpactKids.Bible")]
public interface IBibleService
{
    Task<BasicReadMultipleResponse<BibleVerse>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext context = default
    );
}