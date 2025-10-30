using System.Diagnostics.CodeAnalysis;

namespace GSBC.ImpactKids.Grpc.Services.EventServices.Internal;

public class EventServicesService(IServiceProvider services)
{
    private readonly Dictionary<Guid, KeyedEventService> _services = new();

    public KeyedEventService? GetKeyedEventService(Guid guid, [DoesNotReturnIf(true)] bool createIfNull = true)
    {
        if (_services.TryGetValue(guid, out KeyedEventService? service))
            return service;

        if (!createIfNull)
            return null;
        
        KeyedEventService eventService = services.GetRequiredService<KeyedEventService>();
        eventService.StreamId = guid;
        _services[guid] = eventService;
        return eventService;
    }
    
    public async Task RemoveKeyedEventService(Guid guid)
    {
        if (_services.Remove(guid, out KeyedEventService? service))
        {
            await service.DisposeAsync();
        }
    }
}