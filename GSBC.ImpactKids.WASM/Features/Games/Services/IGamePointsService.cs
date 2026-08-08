using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

namespace GSBC.ImpactKids.WASM.Features.Games.Services;

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

    Task FlushAsync();
}
