using System.Net.ServerSentEvents;
using System.Threading.Channels;

namespace GSBC.ImpactKids.Grpc.Features.Eventing.Services;

public partial class EventingChannelsService(
    ILogger<EventingChannelsService> logger
)
{
    private readonly SemaphoreSlim                     _semaphore = new(1);
    private readonly Dictionary<Guid, EventingChannel> _channels  = new();

    public async Task SendHeartbeat()
    {
        logger.LogInformation("Fanning Out Heartbeat");
        await _semaphore.WaitAsync();
        try
        {
            foreach ((Guid streamId, EventingChannel value) in _channels)
            {
                LogHeartbeatSending(logger, streamId);
                await value.Channel.Writer.WaitToWriteAsync();
                await value.Channel.Writer.WriteAsync(new SseItem<string>("", "heartbeat"));
                LogHeartbeatSent(logger, streamId);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<EventingChannel?> GetChannel(
        Guid              streamId,
        CancellationToken token = default
    )
    {
        await _semaphore.WaitAsync(token);
        try
        {
            _channels[streamId] = new EventingChannel(
                streamId,
                this,
                Channel.CreateBounded<SseItem<string>>(32)
            );

            await _channels[streamId].Channel.Writer
                .WriteAsync(new SseItem<string>("", eventType: "heartbeat"), token);

            await _channels[streamId].Channel.Writer
                .WriteAsync(new SseItem<string>(streamId.ToString(), eventType: "message"), token);

            return _channels[streamId];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task FanoutEvent(
        string data
    )
    {
        await _semaphore.WaitAsync();
        try
        {
            LogFanoutBeginningForEventData(logger, data);
            List<Task> tasks = [];
            foreach ((Guid streamId, EventingChannel channel) in _channels)
            {
                tasks.Add(Task.Run(async () =>
                {
                    LogMessageSending(logger, streamId);
                    await channel.Channel.Writer.WaitToWriteAsync();
                    await channel.Channel.Writer.WriteAsync(new SseItem<string>(data, "message"));
                    LogMessageSent(logger, streamId);
                }));
            }

            await Task.WhenAll(tasks);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveChannel(Guid streamId)
    {
        await _semaphore.WaitAsync();
        try
        {
            LogChannelClosedRemovingFromListStreamId(logger, streamId);
            _channels.Remove(streamId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    [LoggerMessage(LogLevel.Debug, "Heartbeat Sending To {StreamId}")]
    static partial void LogHeartbeatSending(ILogger<EventingChannelsService> logger, Guid streamId);

    [LoggerMessage(LogLevel.Debug, "Heartbeat Sent To {StreamId}")]
    static partial void LogHeartbeatSent(ILogger<EventingChannelsService> logger, Guid streamId);

    [LoggerMessage(LogLevel.Debug, "Message Sending To {StreamId}")]
    static partial void LogMessageSending(ILogger<EventingChannelsService> logger, Guid streamId);

    [LoggerMessage(LogLevel.Debug, "Message Sent To {StreamId}")]
    static partial void LogMessageSent(ILogger<EventingChannelsService> logger, Guid streamId);

    [LoggerMessage(LogLevel.Debug, "Fanout Beginning for event: {Data}")]
    static partial void LogFanoutBeginningForEventData(ILogger<EventingChannelsService> logger, string data);

    [LoggerMessage(LogLevel.Debug, "Channel closed, removing from list: {StreamId}")]
    static partial void LogChannelClosedRemovingFromListStreamId(
        ILogger<EventingChannelsService> logger,
        Guid                             streamId
    );
}

public class EventingChannel(
    Guid                     streamId,
    EventingChannelsService  eventingChannelsService,
    Channel<SseItem<string>> channel
) : IAsyncDisposable
{
    public Channel<SseItem<string>> Channel { get; set; } = channel;

    public async ValueTask DisposeAsync()
    {
        await eventingChannelsService.RemoveChannel(streamId);
        GC.SuppressFinalize(this);
    }
}