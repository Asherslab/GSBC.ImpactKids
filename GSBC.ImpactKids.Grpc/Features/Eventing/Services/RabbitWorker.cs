using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GSBC.ImpactKids.Grpc.Features.Eventing.Services;

public class RabbitWorker(
    IConnection             connection,
    EventingChannelsService eventingChannelsService
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        IChannel       channel = await connection.CreateChannelAsync(cancellationToken: token);
        QueueDeclareOk results = await channel.QueueDeclareAsync(cancellationToken: token);

        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += HandleEvent;
        await channel.BasicConsumeAsync(results.QueueName, autoAck: true, consumer, cancellationToken: token);

        await channel.QueueBindAsync(
            queue: results.QueueName,
            exchange: "events",
            string.Empty,
            cancellationToken: token
        );
    }

    private async Task HandleEvent(object obj, BasicDeliverEventArgs args)
    {
        await eventingChannelsService.FanoutEvent(Encoding.UTF8.GetString(args.Body.ToArray()));
    }
}