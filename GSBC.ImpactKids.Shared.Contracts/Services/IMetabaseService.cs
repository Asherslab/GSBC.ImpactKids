using GSBC.ImpactKids.Shared.Contracts.Messages.Requests;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.Metabase")]
public interface IMetabaseService
{
    Task<MetabaseJwtResponse?> GetMetabaseJwt(
        MetabaseJwtRequest request,
        CallContext        context = default
    );
}