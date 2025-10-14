using System.Text;
using RabbitMQ.Client;

namespace GSBC.ImpactKids.Grpc.Services;

// ReSharper disable once UnusedTypeParameter
public interface IEventService<T>
{
    Task SendUpdatedEvent(Guid id, CancellationToken token = default, params Guid[] topicParentIds);
}

public class EventService<T>(
    IConnection connection
) : IEventService<T>
{
    public async Task SendUpdatedEvent(Guid id, CancellationToken token = default, params Guid[] topicParentIds)
    {
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: token);
        await channel.ExchangeDeclareAsync("data-events", ExchangeType.Topic, cancellationToken: token);
        
        StringBuilder topic = new();
        topic.Append(typeof(T).Name);
        if (topicParentIds.Length != 0)
        {
            foreach (Guid topicParentId in topicParentIds)
            {
                topic.Append($".{topicParentId}");
            }
        }
        topic.Append($".{id}");
        
        await channel.BasicPublishAsync(exchange: "data-events", topic.ToString(), "event"u8.ToArray(), cancellationToken: token);
    }
}