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

    protected async Task SubscribeToEvent(string topic, Func<Task> callOnEvent)
    {
        if (_channel == null || _queueName == null)
        {
            await CreateChannel(callOnEvent);
            if (_channel == null || _queueName == null)
                throw new InvalidOperationException("Channel and QueueName are still null after attempting to create!");
        }
        
        await _channel.QueueBindAsync(queue: _queueName, exchange: "data-events", routingKey: topic);
    }

    private async Task CreateChannel(Func<Task> callOnEvent)
    {
        _channel = await Connection.CreateChannelAsync();
        QueueDeclareOk results = await _channel.QueueDeclareAsync();
        _queueName = results.QueueName;

        AsyncEventingBasicConsumer consumer = new(_channel);
        consumer.ReceivedAsync += async (_, _) => { await InvokeAsync(callOnEvent); };
        await _channel.BasicConsumeAsync(_queueName, autoAck: true, consumer);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null) await _channel.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}