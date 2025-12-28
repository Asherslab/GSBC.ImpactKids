using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Eventing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;

namespace GSBC.ImpactKids.Grpc.Features.Eventing.Api.EventingServices;

[Authorize(Policy = Policies.EnabledOnly)]
public class EventingService(
    IDistributedCache distributedCache
) : IEventingService
{
    [Authorize(Policy = Policies.EnabledOnly)]
    public async Task<BasicReadResponse<Guid>> GetStreamId(CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        Guid streamId = Guid.NewGuid();

        await distributedCache.SetAsync(
            $"stream-id-{streamId}",
            [],
            new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromDays(1)
            },
            token
        );

        return new BasicReadResponse<Guid>
        {
            Entity = streamId,
            Success = true
        };
    }
}