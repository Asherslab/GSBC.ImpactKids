using System.Text;
using GSBC.ImpactKids.Grpc.Features.Games.GameDisplayServices;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GSBC.ImpactKids.Grpc.Features.Eventing.Services;

public class RabbitWorker(
    IConnection             connection,
    EventingChannelsService eventingChannelsService,
    GameDataChangeNotifier  gameDataChangeNotifier,
    ILogger<RabbitWorker>   logger
) : BackgroundService
{
    /// <summary>
    /// Entity types a watching wall display cares about. Anything else fans out to the
    /// signed in clients only.
    /// </summary>
    private static readonly HashSet<string> ScoreboardTypes =
    [
        typeof(GamePointRecord).FullName!,
        typeof(GameBoard).FullName!
    ];


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
        logger.LogDebug("RabbitMQ Event Received");

        string entityType = Encoding.UTF8.GetString(args.Body.ToArray());

        if (ScoreboardTypes.Contains(entityType))
            gameDataChangeNotifier.NotifyChanged();

        await eventingChannelsService.FanoutEvent(entityType);
    }
}