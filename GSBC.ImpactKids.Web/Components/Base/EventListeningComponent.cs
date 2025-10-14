using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GSBC.ImpactKids.Web.Components.Base;

public class EventListeningComponent : ComponentBase, IAsyncDisposable
{
    [Inject]
    protected IConnection Connection { get; set; } = null!;

    private IChannel? _channel;
    private string?   _queueName;

    private readonly List<Callback> _callbacks = [];

    private class Callback
    {
        public required Regex      TopicMatcher { get; init; }
        public required Func<Task> CallOnEvent  { get; init; }
    }

    protected async Task SubscribeToEvent(string topic, Func<Task> callOnEvent)
    {
        if (_channel == null || _queueName == null)
        {
            await CreateChannel();
            if (_channel == null || _queueName == null)
                throw new InvalidOperationException("Channel and QueueName are still null after attempting to create!");
        }

        string regexMatch = topic.Replace("*", "([^.]+)").Replace("#", "([^.]+.?)+");
        regexMatch = $"^{regexMatch}$";
        Regex topicMatcher = new(regexMatch);

        _callbacks.Add(new Callback
            {
                TopicMatcher = topicMatcher,
                CallOnEvent = callOnEvent
            }
        );

        await _channel.QueueBindAsync(queue: _queueName, exchange: "data-events", routingKey: topic);
    }

    private async Task CreateChannel()
    {
        _channel = await Connection.CreateChannelAsync();
        QueueDeclareOk results = await _channel.QueueDeclareAsync();
        _queueName = results.QueueName;

        AsyncEventingBasicConsumer consumer = new(_channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            foreach (
                Callback callback in _callbacks
                    .Where(callback => callback.TopicMatcher.IsMatch(eventArgs.RoutingKey))
            )
            {
                await InvokeAsync(callback.CallOnEvent);
            }
        };
        await _channel.BasicConsumeAsync(_queueName, autoAck: true, consumer);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null) await _channel.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}