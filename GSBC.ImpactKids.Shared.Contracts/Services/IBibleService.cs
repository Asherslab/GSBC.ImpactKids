using GSBC.ImpactKids.Shared.Contracts.Entities.Bible;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.Bible")]
public interface IBibleService
{
    Task<BasicReadMultipleResponse<BibleVerse>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext context = default
    );
}