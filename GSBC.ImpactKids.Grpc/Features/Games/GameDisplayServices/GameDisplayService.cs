using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Games;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
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
    GsbcDbContext db
) : IGameDisplayService
{
    public async Task<GameScoreboardResponse> GetScoreboard(
        GameScoreboardRequest request,
        CallContext           context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbService? service = await ResolveServiceAsync(request.ServiceId, token);

        if (service == null)
            return GameScoreboardResponse.WithError(ServiceNotFound);

        DbGameBoard? board = await db.GameBoards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ServiceId == service.Id, token);

        int             teamCount   = board?.TeamCount ?? 4;
        bool            hidden      = board?.Hidden ?? false;
        bool            paused      = board?.Paused ?? false;
        int             currentGame = board?.CurrentGame ?? 1;
        GameDisplayMode mode        = board?.DisplayMode ?? GameDisplayMode.Totals;

        if (hidden)
            return new GameScoreboardResponse
            {
                Success = true,
                Title = service.Name,
                Hidden = true,
                Paused = paused,
                Mode = mode,
                CurrentGame = currentGame,
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

        ImmutableList<TeamScoreLine> teams = Enum.GetValues<GameTeam>()
            .Take(teamCount)
            .Select(team =>
                {
                    int gamePoints = records
                        .Where(x => x.Team == team && x.GameNumber != null)
                        .Sum(x => x.Points);

                    int behaviourPoints = records
                        .Where(x => x.Team == team && x.GameNumber == null)
                        .Sum(x => x.Points);

                    int currentGamePoints = records
                        .Where(x => x.Team == team && x.GameNumber == currentGame)
                        .Sum(x => x.Points);

                    return new TeamScoreLine
                    {
                        Team = team,
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
            .ThenBy(x => x.Team)
            .ToImmutableList();

        return new GameScoreboardResponse
        {
            Success = true,
            Title = service.Name,
            Hidden = false,
            Paused = paused,
            Mode = mode,
            CurrentGame = currentGame,
            Teams = teams
        };
    }

    private async Task<DbService?> ResolveServiceAsync(Guid? serviceId, CancellationToken token)
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
