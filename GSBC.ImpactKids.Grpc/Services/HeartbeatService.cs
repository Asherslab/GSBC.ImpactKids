using GSBC.ImpactKids.Grpc.Features.Eventing.Services;

namespace GSBC.ImpactKids.Grpc.Services;

public class HeartbeatService(
    EventingChannelsService   eventingChannelsService,
    ILogger<HeartbeatService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            PeriodicTimer timer = new(TimeSpan.FromSeconds(15));
            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogDebug("Sending Heartbeat");
                await eventingChannelsService.SendHeartbeat();
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Heartbeat worker failed!");
            throw;
        }
    }
}