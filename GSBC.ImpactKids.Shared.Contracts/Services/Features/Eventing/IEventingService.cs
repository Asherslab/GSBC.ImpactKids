namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Eventing;

[Service("GSBC.ImpactKids.Eventing")]
public interface IEventingService
{
    public Task<BasicReadResponse<Guid>> GetStreamId(CallContext context = default);
}