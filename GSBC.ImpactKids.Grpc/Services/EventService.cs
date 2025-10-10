using RabbitMQ.Client;

namespace GSBC.ImpactKids.Grpc.Services;

public interface IEventService<T>
{
    Task SendUpdatedEvent(Guid id, string[]? additionalTopics = null, CancellationToken token = default);
}

public class EventService<T>(
    IConnection connection
) : IEventService<T>
{
    public async Task SendUpdatedEvent(Guid id, string[]? additionalTopics = null, CancellationToken token = default)
    {
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: token);
        await channel.ExchangeDeclareAsync("data-events", ExchangeType.Topic, cancellationToken: token);
        await channel.BasicPublishAsync(exchange: "data-events", $"{typeof(T).Name}.{id}", "event"u8.ToArray(), cancellationToken: token);
        if (additionalTopics != null)
        {
            foreach (string additionalTopic in additionalTopics)
            {
                await channel.BasicPublishAsync(exchange: "data-events", additionalTopic, "event"u8.ToArray(), cancellationToken: token);
            }
        }
    }
}