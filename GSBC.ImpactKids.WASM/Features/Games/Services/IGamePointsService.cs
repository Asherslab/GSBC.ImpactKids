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

    /// <summary>Game points plus behaviour points.</summary>
    int TotalFor(Guid serviceId, GameTeam team);

    int GamePointsFor(Guid serviceId, GameTeam team);

    int GamePointsFor(Guid serviceId, GameTeam team, int gameNumber);

    int BehaviourPointsFor(Guid serviceId, GameTeam team);

    bool CanUndo(Guid serviceId);

    /// <summary>Awards points in the current game.</summary>
    Task AddGamePointsAsync(Guid serviceId, GameTeam team, int points);

    /// <summary>Awards points outside any game. Still counts toward the total.</summary>
    Task AddBehaviourPointsAsync(Guid serviceId, GameTeam team, int points);

    Task UndoLastAsync(Guid serviceId);

    Task FlushAsync();
}
