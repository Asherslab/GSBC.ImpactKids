using GSBC.ImpactKids.Shared.Contracts.Entities.Bible;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.Bible")]
public interface IBibleService
{
    Task<BasicReadMultipleResponse<BibleVerse>> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext context = default
    );
}