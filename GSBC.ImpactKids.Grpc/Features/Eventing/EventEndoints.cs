using System.Net.ServerSentEvents;
using System.Threading.Channels;
using GSBC.ImpactKids.Grpc.Features.Eventing.Services;
using Microsoft.AspNetCore.Mvc;

namespace GSBC.ImpactKids.Grpc.Features.Eventing;

public static class EventEndoints
{
    public static IEndpointRouteBuilder AddEventEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("stream", Stream);

        return group;
    }

    private static async Task<IResult> Stream(
        [FromQuery(Name = "StreamId")] Guid? streamId,
        EventingChannelsService              eventingChannelsService,
        CancellationToken                    token = default
    )
    {
        if (streamId == null)
            return Results.NotFound();

        Channel<SseItem<string>>? channel = await eventingChannelsService.GetChannel(streamId.Value, token);
        if (channel == null)
            return Results.NotFound();

        return Results.ServerSentEvents(
            channel.Reader.ReadAllAsync(token)
        );
    }
}