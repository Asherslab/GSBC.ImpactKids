using GSBC.ImpactKids.Grpc.Services.EventServices.Internal;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Events;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Services.EventServices;

[Authorize(Policy = Policies.EnabledOnly)]
public class EventService(
    EventServicesService eventServices
) : IEventService
{
    public async IAsyncEnumerable<EventResponse> Stream(EventStreamRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        await using KeyedEventService eventService = eventServices.GetKeyedEventService(request.StreamId);

        yield return new EventResponse
        {
            RoutingKey = null
        };
        while (!token.IsCancellationRequested)
        {
            await foreach (EventResponse eventResponse in eventService.Stream(token))
            {
                yield return eventResponse;
            }
        }
    }

    public async Task<BasicReadResponse<Guid>> Bind(EventBindRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        KeyedEventService? eventService = eventServices.GetKeyedEventService(request.StreamId, false);
        if (eventService == null)
            return BasicReadResponse<Guid>.WithError(EventStreamIdNotFound);
        
        Guid? subscriptionId = await eventService.Bind(request.Topic, token);
        if (subscriptionId == null)
            return BasicReadResponse<Guid>.WithError(EventStreamNotRunning);
        
        return new BasicReadResponse<Guid> { Success = true, Entity = subscriptionId.Value };
    }

    public async Task<BasicResponse> Unbind(EventUnbindRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        KeyedEventService? eventService = eventServices.GetKeyedEventService(request.StreamId, false);
        if (eventService == null)
            return BasicResponse.WithError(EventStreamIdNotFound);
        await eventService.Unbind(request.SubscriptionId, token);
        return new BasicResponse { Success = true };
    }

    // public async Task<BasicResponse> UnbindAll(EventUnbindAllRequest request, CallContext context = default)
    // {
    //     CancellationToken token = context.CancellationToken;
    //
    //     KeyedEventService? eventService = eventServices.GetKeyedEventService(request.StreamId, false);
    //     if (eventService == null)
    //         return BasicResponse.WithError(EventStreamIdNotFound);
    //     await eventService.UnbindAll(request.TopicMatcher, token);
    //     return new BasicResponse { Success = true };
    // }
}