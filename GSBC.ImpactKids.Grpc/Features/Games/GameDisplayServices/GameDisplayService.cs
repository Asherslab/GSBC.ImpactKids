using System.Collections.Immutable;
using System.Text;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Games;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Games;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Games.GameDisplayServices;

/// <summary>
/// Unauthenticated on purpose - see <see cref="IGameDisplayService"/>. Returns
/// aggregate team scores only; never add anything person shaped to this response.
/// </summary>
public class GameDisplayService(
    GsbcDbContext                  db,
    IDbContextFactory<GsbcDbContext> dbFactory,
    GameDataChangeNotifier         changes
) : IGameDisplayService
{
    /// <summary>
    /// How long a watcher sits waiting for a change before it looks anyway. Also the
    /// upper bound on how stale a board can be if a change event is ever missed.
    /// </summary>
    private static readonly TimeSpan WatchTick = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Resend the board at least this often even when nothing has changed. A wall display
    /// behind a proxy has no other way to notice its stream has quietly died.
    /// </summary>
    private static readonly TimeSpan KeepAlive = TimeSpan.FromSeconds(30);

    public async Task<GameScoreboardResponse> GetScoreboard(
        GameScoreboardRequest request,
        CallContext           context = default
    ) => await BuildScoreboardAsync(db, request.ServiceId, context.CancellationToken);

    /// <summary>
    /// Pushes the board on every change instead of making the display ask. The first item
    /// is the current board, so a caller never needs a separate read to paint the screen.
    /// </summary>
    public async IAsyncEnumerable<GameScoreboardResponse> WatchScoreboard(
        GameScoreboardRequest request,
        CallContext           context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        string?         lastSignature = null;
        DateTimeOffset  lastSent      = DateTimeOffset.MinValue;

        while (!token.IsCancellationRequested)
        {
            // Claimed before the read, so a write that lands while we are reading still
            // wakes the wait below instead of sitting until the next tick.
            GameDataChangeSubscription pending = changes.Subscribe();

            // A fresh context per look: this call outlives any sane scoped lifetime.
            GameScoreboardResponse board = await dbFactory.RunWithNewDbContext(
                context => BuildScoreboardAsync(context, request.ServiceId, token),
                token
            );

            string signature = Signature(board);

            if (signature != lastSignature || DateTimeOffset.UtcNow - lastSent >= KeepAlive)
            {
                lastSignature = signature;
                lastSent = DateTimeOffset.UtcNow;

                yield return board;
            }

            await pending.WaitAsync(WatchTick, token);
        }
    }

    /// <summary>
    /// Everything the display would actually render, flattened. Two boards with the same
    /// signature look identical on the wall, so there is no point sending the second.
    /// </summary>
    private static string Signature(GameScoreboardResponse board)
    {
        StringBuilder builder = new();

        builder.Append(board.Success).Append('|')
            .Append(board.Error).Append('|')
            .Append(board.Title).Append('|')
            .Append(board.Hidden).Append('|')
            .Append(board.Paused).Append('|')
            .Append((int)board.Mode).Append('|')
            .Append(board.CurrentGame).Append('|')
            .Append(board.CurrentGameName).Append('|')
            .Append(board.CurrentGameHasAlliances);

        foreach (TeamScoreLine line in board.Teams)
        {
            builder.Append('|')
                .Append(line.TeamIndex).Append(':')
                .Append(line.Name).Append(':')
                .Append(line.Colour).Append(':')
                .Append(line.AllianceGroup).Append(':')
                .Append(line.DisplayPoints);
        }

        return builder.ToString();
    }

    private static async Task<GameScoreboardResponse> BuildScoreboardAsync(
        GsbcDbContext     db,
        Guid?             serviceId,
        CancellationToken token
    )
    {
        DbService? service = await ResolveServiceAsync(db, serviceId, token);

        if (service == null)
            return GameScoreboardResponse.WithError(ServiceNotFound);

        DbGameBoard? board = await db.GameBoards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ServiceId == service.Id, token);

        bool            hidden      = board?.Hidden ?? false;
        bool            paused      = board?.Paused ?? false;
        int             currentGame = board?.CurrentGame ?? 1;
        GameDisplayMode mode        = board?.DisplayMode ?? GameDisplayMode.Totals;

        List<DbGameTeam> teamDefinitions = board is { Teams.Count: > 0 }
            ? board.Teams.OrderBy(x => x.Index).ToList()
            : [.. GameTeamDefaults.Default().Select(x => new DbGameTeam
                {
                    Index = x.Index,
                    Name = x.Name,
                    Colour = x.Colour
                }
            )];

        DbGame? game = board?.Games.FirstOrDefault(x => x.Number == currentGame);

        // Positional, matching the team list. Teams sharing a value played this game
        // combined; anyone outside the list gets a group of their own.
        List<int> alliances = game?.Alliances.Count == teamDefinitions.Count
            ? game!.Alliances
            : [];

        bool hasAlliances = alliances.Count > 0 && alliances.Distinct().Count() < alliances.Count;

        string currentGameName = string.IsNullOrWhiteSpace(game?.Name)
            ? $"Game {currentGame}"
            : game!.Name!;

        if (hidden)
            return new GameScoreboardResponse
            {
                Success = true,
                Title = service.Name,
                Hidden = true,
                Paused = paused,
                Mode = mode,
                CurrentGame = currentGame,
                CurrentGameName = currentGameName,
                CurrentGameHasAlliances = hasAlliances,
                Teams = []
            };

        IQueryable<DbGamePointRecord> query = db.GamePointRecords
            .AsNoTracking()
            .Where(x => x.ServiceId == service.Id && !x.Deleted);

        // While paused the board is frozen: anything awarded since the pause began
        // is still recorded, it just does not reach the wall until play resumes.
        if (paused && board?.PausedAt != null)
            query = query.Where(x => x.Awarded <= board.PausedAt);

        List<DbGamePointRecord> records = await query.ToListAsync(token);

        ImmutableList<TeamScoreLine> teams = teamDefinitions
            .Select(team =>
                {
                    int gamePoints = records
                        .Where(x => x.TeamIndex == team.Index && x.GameNumber != null)
                        .Sum(x => x.Points);

                    int behaviourPoints = records
                        .Where(x => x.TeamIndex == team.Index && x.GameNumber == null)
                        .Sum(x => x.Points);

                    int currentGamePoints = records
                        .Where(x => x.TeamIndex == team.Index && x.GameNumber == currentGame)
                        .Sum(x => x.Points);

                    return new TeamScoreLine
                    {
                        TeamIndex = team.Index,
                        Name = team.Name,
                        Colour = team.Colour,
                        AllianceGroup = team.Index < alliances.Count
                            ? alliances[team.Index]
                            : -1 - team.Index,
                        DisplayPoints = mode == GameDisplayMode.CurrentGame
                            ? currentGamePoints
                            : gamePoints + behaviourPoints,
                        GamePoints = gamePoints,
                        BehaviourPoints = behaviourPoints,
                        CurrentGamePoints = currentGamePoints
                    };
                }
            )
            .OrderByDescending(x => x.DisplayPoints)
            .ThenBy(x => x.TeamIndex)
            .ToImmutableList();

        return new GameScoreboardResponse
        {
            Success = true,
            Title = service.Name,
            Hidden = false,
            Paused = paused,
            Mode = mode,
            CurrentGame = currentGame,
            CurrentGameName = currentGameName,
            CurrentGameHasAlliances = hasAlliances,
            Teams = teams
        };
    }

    private static async Task<DbService?> ResolveServiceAsync(
        GsbcDbContext     db,
        Guid?             serviceId,
        CancellationToken token
    )
    {
        if (serviceId != null)
            return await db.Services
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == serviceId, token);

        // No id given, so the display is on a fixed URL - fall back to today, then
        // to the most recent service, matching how the scoring tool picks one.
        DateTimeOffset todayStart = new(DateTime.UtcNow.Date, TimeSpan.Zero);
        DateTimeOffset todayEnd   = todayStart.AddDays(1);

        DbService? today = await db.Services
            .AsNoTracking()
            .Where(x => x.Date >= todayStart && x.Date < todayEnd)
            .OrderBy(x => x.Date)
            .FirstOrDefaultAsync(token);

        return today ?? await db.Services
            .AsNoTracking()
            .OrderByDescending(x => x.Date)
            .FirstOrDefaultAsync(token);
    }
}
