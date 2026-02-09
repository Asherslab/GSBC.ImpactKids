using GSBC.ImpactKids.Grpc.Features.Eventing.Services;

namespace GSBC.ImpactKids.Grpc.Services;

public class HeartbeatService(
    EventingChannelsService eventingChannelsService
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PeriodicTimer timer = new(TimeSpan.FromSeconds(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            await eventingChannelsService.SendHeartbeat();
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}