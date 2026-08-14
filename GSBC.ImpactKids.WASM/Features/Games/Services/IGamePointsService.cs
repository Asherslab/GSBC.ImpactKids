using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

namespace GSBC.ImpactKids.WASM.Features.Games.Services;

// GameRound and GamePlacementAward live in the feature root, alongside the placement maths.

public interface IGamePointsService : IAsyncDisposable
{
    /// <summary>Raised whenever totals, board settings, pending count or connectivity change.</summary>
    event Action? Changed;

    bool IsOnline     { get; }
    bool Initialised  { get; }
    int  PendingCount { get; }

    Task InitialiseAsync();

    /// <summary>Board settings for a service, falling back to defaults if none exist yet.</summary>
    GameBoard BoardFor(Guid serviceId);

    Task UpdateBoardAsync(Guid serviceId, Func<GameBoard, GameBoard> mutate);

    /// <summary>Games actually played - the higher of the current game and the highest scored in.</summary>
    int GamesPlayed(Guid serviceId);

    /// <summary>Whether anything has been scored in a given game yet.</summary>
    bool HasScores(Guid serviceId, int gameNumber);

    /// <summary>Game points plus behaviour points.</summary>
    int TotalFor(Guid serviceId, int teamIndex);

    int GamePointsFor(Guid serviceId, int teamIndex);

    int GamePointsFor(Guid serviceId, int teamIndex, int gameNumber);

    int BehaviourPointsFor(Guid serviceId, int teamIndex);

    bool CanUndo(Guid serviceId);

    /// <summary>
    /// Awards points in the current game. Passing more than one team scores a combined
    /// side: every team named gets the full amount, and undo takes the lot back.
    /// </summary>
    Task AddGamePointsAsync(Guid serviceId, IReadOnlyList<int> teamIndexes, int points);

    /// <summary>Awards points outside any game. Still counts toward the total.</summary>
    Task AddBehaviourPointsAsync(Guid serviceId, int teamIndex, int points);

    Task UndoLastAsync(Guid serviceId);

    /// <summary>
    /// The heats of one game, oldest first. Rounds all share the game's number, so the
    /// tally and the reveal go on seeing one game with one total.
    /// </summary>
    IReadOnlyList<GameRound> RoundsFor(Guid serviceId, int gameNumber);

    /// <summary>Rounds for every game of the night that has any, keyed by game number.</summary>
    IReadOnlyDictionary<int, IReadOnlyList<GameRound>> RoundsByGame(Guid serviceId);

    /// <summary>
    /// Scores a whole heat at once: one record per placed team, sharing a group id so the
    /// round can be undone, edited or read back as a finishing order.
    /// </summary>
    Task AwardPlacementAsync(Guid serviceId, int gameNumber, IReadOnlyList<GamePlacementAward> awards);

    /// <summary>Rewrites a round that was already awarded, keeping its place among the heats.</summary>
    Task ReplaceRoundAsync(
        Guid                              serviceId,
        int                               gameNumber,
        Guid                              roundKey,
        IReadOnlyList<GamePlacementAward> awards
    );

    Task DeleteRoundAsync(Guid serviceId, Guid roundKey);

    /// <summary>
    /// Corrects what a team scored in a tapped game by writing the difference, so the
    /// records stay append only and a laptop correction merges with a phone still scoring.
    /// </summary>
    Task SetGamePointsAsync(Guid serviceId, int teamIndex, int gameNumber, int points);

    Task SetBehaviourPointsAsync(Guid serviceId, int teamIndex, int points);

    /// <summary>Takes back everything scored in a game - for deleting one outright.</summary>
    Task ClearGameAsync(Guid serviceId, int gameNumber);

    Task FlushAsync();

    /// <summary>
    /// Everything a stuck queue might need, in order: ask the browser whether it is
    /// really offline, send whatever is waiting, then re-read the server. Exists because
    /// the only thing anyone can do about a stalled sync otherwise is reload the page.
    /// </summary>
    Task ResyncAsync();
}
