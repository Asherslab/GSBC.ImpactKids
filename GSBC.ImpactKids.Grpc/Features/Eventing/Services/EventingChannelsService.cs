using System.Net.ServerSentEvents;
using System.Threading.Channels;
using Microsoft.Extensions.Caching.Distributed;

namespace GSBC.ImpactKids.Grpc.Features.Eventing.Services;

public class EventingChannelsService(
    IDistributedCache distributedCache
)
{
    private readonly Dictionary<Guid, Channel<SseItem<string>>> _channels = new();

    public async Task<Channel<SseItem<string>>?> GetChannel(
        Guid              streamId,
        CancellationToken token = default
    )
    {
        byte[]? streamIdEntry = await distributedCache.GetAsync(
            $"stream-id-{streamId}",
            token
        );

        bool streamIdExists = streamIdEntry != null;

        if (!streamIdExists)
            return null;

        _channels[streamId] = Channel.CreateBounded<SseItem<string>>(32); // doesn't need many
        await _channels[streamId].Writer.WriteAsync(new SseItem<string>("", eventType: "heartbeat"), token);
        return _channels[streamId];
    }

    public async Task FanoutEvent(
        string data
    )
    {
        List<Task> tasks = [];
        foreach ((Guid _, Channel<SseItem<string>> channel) in _channels)
        {
            tasks.Add(Task.Run(async () =>
            {
                await channel.Writer.WaitToWriteAsync();
                await channel.Writer.WriteAsync(new SseItem<string>(data));
            }));
        }

        await Task.WhenAll(tasks);
    }
}