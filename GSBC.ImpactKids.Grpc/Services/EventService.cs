using System.Text;
using RabbitMQ.Client;

namespace GSBC.ImpactKids.Grpc.Services;

// ReSharper disable once UnusedTypeParameter
public interface IEventService<T>
{
    Task SendUpdatedEvent(CancellationToken         token = default);
    Task SendUpdatedEvent<TOther>(CancellationToken token = default);
}

public class EventService<T>(
    IConnection connection
) : IEventService<T>
{
    public async Task SendUpdatedEvent(CancellationToken token = default)
    {
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: token);
        // await channel.ExchangeDeclareAsync("data-events", ExchangeType.Topic, cancellationToken: token);

        await channel.BasicPublishAsync(exchange: "events", string.Empty, Encoding.UTF8.GetBytes(typeof(T).FullName!),
            cancellationToken: token);
    }

    public async Task SendUpdatedEvent<TOther>(CancellationToken token = default)
    {
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: token);
        // await channel.ExchangeDeclareAsync("data-events", ExchangeType.Topic, cancellationToken: token);

        await channel.BasicPublishAsync(exchange: "events", string.Empty,
            Encoding.UTF8.GetBytes(typeof(TOther).FullName!),
            cancellationToken: token);
    }
}