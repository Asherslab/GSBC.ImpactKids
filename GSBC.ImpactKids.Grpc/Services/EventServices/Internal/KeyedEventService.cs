using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GSBC.ImpactKids.Grpc.Services.EventServices.Internal;

public class KeyedEventService(
    IConnection          connection,
    EventServicesService eventServicesService
) : IAsyncDisposable
{
    public Guid StreamId { get; set; }

    private readonly Channel<BasicDeliverEventArgs?> _eventsChannel = Channel.CreateUnbounded<BasicDeliverEventArgs?>();
    private          Timer?                          _keepAliveTimer;

    private IChannel? _channel;
    private string?   _queueName;

    public async IAsyncEnumerable<EventResponse> Stream(
        [EnumeratorCancellation] CancellationToken token = default
    )
    {
        _channel = await connection.CreateChannelAsync(cancellationToken: token);
        QueueDeclareOk results = await _channel.QueueDeclareAsync(cancellationToken: token);
        _queueName = results.QueueName;

        AsyncEventingBasicConsumer consumer = new(_channel);
        consumer.ReceivedAsync += async (_, eventArgs) => { await _eventsChannel.Writer.WriteAsync(eventArgs, token); };
        await _channel.BasicConsumeAsync(_queueName, autoAck: true, consumer, cancellationToken: token);

        _keepAliveTimer = new Timer(
            _ => _eventsChannel.Writer.TryWrite(null),
            null,
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(15)
        );

        while (!token.IsCancellationRequested)
        {
            await foreach (BasicDeliverEventArgs? args in _eventsChannel.Reader.ReadAllAsync(token))
            {
                yield return new EventResponse
                {
                    RoutingKey = args?.RoutingKey
                };
            }
        }
    }

    private readonly Dictionary<Guid, string> _routingKeys = [];

    public async Task<Guid?> Bind(string routingKey, CancellationToken token = default)
    {
        if (_channel == null || _queueName == null)
            return null;

        if (!_routingKeys.ContainsValue(routingKey))
        {
            await _channel.QueueBindAsync(
                queue: _queueName,
                exchange: "data-events",
                routingKey: routingKey,
                cancellationToken: token
            );
        }

        Guid subscriptionId = Guid.NewGuid();
        _routingKeys[subscriptionId] = routingKey;
        return subscriptionId;
    }

    public async Task Unbind(Guid subscriptionId, CancellationToken token = default)
    {
        if (_channel == null || _queueName == null)
            return;

        _routingKeys.Remove(subscriptionId, out string? routingKey);

        // don't unbind until ALL subscribers are gone
        if (routingKey != null && !_routingKeys.ContainsValue(routingKey))
        {
            await _channel.QueueUnbindAsync(
                queue: _queueName,
                exchange: "data-events",
                routingKey: routingKey,
                cancellationToken: token
            );
        }
    }

    // public async Task UnbindAll(string? topicsToMatch = null, CancellationToken token = default)
    // {
    //     if (_channel == null || _queueName == null)
    //         return;
    //
    //     if (topicsToMatch == null)
    //     {
    //         foreach (string routingKey in _routingKeys.ToList()) // copies the list for iteration and modification
    //         {
    //             await _channel.QueueUnbindAsync(
    //                 queue: _queueName,
    //                 exchange: "data-events",
    //                 routingKey: routingKey,
    //                 cancellationToken: token
    //             );
    //             _routingKeys.Remove(routingKey);
    //         }
    //
    //         return;
    //     }
    //
    //     string regexMatch = topicsToMatch.Replace("*", "([^.]+)").Replace("#", "([^.]+.?)+");
    //     regexMatch = $"^{regexMatch}$";
    //     Regex topicMatcher = new(regexMatch);
    //
    //     foreach (string routingKey in _routingKeys
    //                  .Where(x => topicMatcher.IsMatch(x))
    //                  .ToList()
    //             )
    //     {
    //         await _channel.QueueUnbindAsync(
    //             queue: _queueName,
    //             exchange: "data-events",
    //             routingKey: routingKey,
    //             cancellationToken: token
    //         );
    //         _routingKeys.Remove(routingKey);
    //     }
    // }

    public async ValueTask DisposeAsync()
    {
        if (_keepAliveTimer != null)
            await _keepAliveTimer.DisposeAsync();

        await eventServicesService.RemoveKeyedEventService(StreamId);

        if (_channel != null)
            await _channel.DisposeAsync();

        _eventsChannel.Writer.TryComplete();

        GC.SuppressFinalize(this);
    }
}