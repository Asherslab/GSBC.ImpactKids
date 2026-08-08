using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Games;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Games;

/// <summary>
/// Read only, unauthenticated, for the wall display.
/// <para>
/// Deliberately routed under "public/" rather than "gRPC/" - the reverse proxy
/// requires a signed in cookie for everything under "gRPC/", and a screen on a
/// wall cannot log in. Only ever expose aggregate scores through here.
/// </para>
/// </summary>
[Service("public/GSBC.ImpactKids.Games.Display")]
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
