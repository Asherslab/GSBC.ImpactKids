using GSBC.ImpactKids.WASM.Services.RefreshableStore;

namespace GSBC.ImpactKids.WASM.Features.Eventing;

public record EventsStreamState(
    bool IsConnected
) : IInitialisableState<EventsStreamState>
{
    public static EventsStreamState Initial => new(false);

    public EventsStreamState SetConnected(bool isConnected) => this with { IsConnected = isConnected };
}