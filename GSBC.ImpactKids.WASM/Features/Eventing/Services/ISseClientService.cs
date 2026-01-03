namespace GSBC.ImpactKids.WASM.Features.Eventing.Services;

public interface ISseClientService : IAsyncDisposable
{
    public bool                  Connected { get; }
    Task                         StartAsync();
    Task                         StopAsync();
    IAsyncEnumerable<SseMessage> GetMessagesAsync(CancellationToken ct = default);
}

public sealed record SseMessage(
    string  Data,
    string? Id,
    string? EventType
);