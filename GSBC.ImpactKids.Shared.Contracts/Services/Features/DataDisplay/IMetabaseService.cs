using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DataDisplay.MetabaseEmbeds;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.DataDisplay;

[Service("gRPC/GSBC.ImpactKids.Metabase")]
public interface IMetabaseService
    : IBasicReadMultipleService<MetabaseEmbed>,
        ICreateService<CreateMetabaseEmbedRequest>,
        IUpdateService<UpdateMetabaseEmbedRequest>,
        IBasicDeleteService<MetabaseEmbed>
{
    /// <summary>An embed token for the stored embed with this id. The response's entity is the token.</summary>
    Task<BasicReadResponse<string?>> GetMetabaseJwt(
        BasicReadRequest request,
        CallContext      context = default
    );
}
