using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Games;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Games;

/// <summary>
/// Read only, for the wall display. Only ever expose aggregate scores through here.
/// <para>
/// Routed under "gRPC/" like everything else. It used to sit under "public/" because it was
/// anonymous and the proxy demanded a leader's cookie on "gRPC/" - neither is true now. A
/// games wall enrols on the display key, the proxy admits either caller on this prefix, and
/// the gRPC service decides what each may do per method: this one is marked
/// <c>EnabledOrDisplay</c>, and a display can never write anywhere.
/// </para>
/// </summary>
[Service("gRPC/GSBC.ImpactKids.Games.Display")]
public interface IGameDisplayService
{
    Task<GameScoreboardResponse> GetScoreboard(
        GameScoreboardRequest request,
        CallContext           context = default
    );

    /// <summary>
    /// Server streaming. Yields the board immediately, then again on every change - the
    /// display does not poll. Also re-sends periodically so a stream killed by something
    /// in between is noticed rather than leaving a frozen screen on the wall.
    /// </summary>
    IAsyncEnumerable<GameScoreboardResponse> WatchScoreboard(
        GameScoreboardRequest request,
        CallContext           context = default
    );
}
