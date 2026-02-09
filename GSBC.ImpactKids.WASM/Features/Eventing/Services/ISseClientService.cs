namespace GSBC.ImpactKids.WASM.Features.Eventing.Services;

public interface ISseClientService : IAsyncDisposable
{
    public bool                  Connected { get; }
    public bool                  Started   { get; }
    Task                         StartAsync();
    Task                         StopAsync();
}

public sealed record SseMessage(
    string  Data,
    string? Id,
    string? EventType
);