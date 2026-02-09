using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using GSBC.ImpactKids.Grpc.Features.Eventing.Services;
using Microsoft.AspNetCore.Mvc;

namespace GSBC.ImpactKids.Grpc.Features.Eventing;

public static class EventEndoints
{
    public static IEndpointRouteBuilder AddEventEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("api/stream", Stream)
            .RequireAuthorization();

        return group;
    }

    private static async Task<IResult> Stream(
        [FromServices] EventingChannelsService eventingChannelsService,
        [FromServices] ILogger                 logger,
        HttpContext                            ctx,
        CancellationToken                      token = default
    )
    {
        if (ctx.User.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        EventingChannel? channel = await eventingChannelsService.GetChannel(Guid.NewGuid(), token);
        if (channel == null)
            return Results.NotFound();

        logger.LogInformation("Connection Established");
        return Results.ServerSentEvents(
            StreamEvents(channel, token)
        );
    }

    private static async IAsyncEnumerable<SseItem<string>> StreamEvents(
        EventingChannel                            channel,
        [EnumeratorCancellation] CancellationToken token = default
    )
    {
        try
        {
            await foreach (SseItem<string> sseItem in channel.Channel.Reader.ReadAllAsync(token))
            {
                yield return sseItem;
            }
        }
        finally
        {
            await channel.DisposeAsync();
        }
    }
}