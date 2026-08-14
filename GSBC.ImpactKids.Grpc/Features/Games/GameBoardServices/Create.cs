using GSBC.ImpactKids.Grpc.Data.Models.Games;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Games.GameBoardServices;

public partial class GameBoardService
{
    /// <summary>
    /// Upserts the single board for a service. Writes older than the stored state
    /// are dropped, so a board edit that was queued offline cannot undo a newer one.
    /// </summary>
    public async Task<BasicReadResponse<Guid?>> Create(
        UpsertGameBoardRequest request,
        CallContext            context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbService? service = await db.Services.FirstOrDefaultAsync(x => x.Id == request.ServiceId, token);

        if (service == null)
            return BasicReadResponse<Guid?>.WithError(ServiceNotFound);

        DateTimeOffset updatedAt = ToUtc(request.UpdatedAt) ?? DateTimeOffset.UtcNow;

        int              currentGame = Math.Max(1, request.CurrentGame);
        List<DbGameTeam> teams       = NormaliseTeams(request.Teams);
        List<DbGame>     games       = NormaliseGames(request.Games, teams.Count);

        DbGameBoard? board = await db.GameBoards
            .FirstOrDefaultAsync(x => x.ServiceId == service.Id, token);

        if (board == null)
        {
            board = new DbGameBoard
            {
                Id = Guid.Empty,

                CurrentGame = currentGame,
                StepPoints = NormalisePoints(request.StepPoints, fallback: 1),
                BonusPoints = NormalisePoints(request.BonusPoints, fallback: 5),
                PointsMultiplier = GameMultipliers.Normalise(request.PointsMultiplier),
                BehaviourPointsMultiplier = GameMultipliers.Normalise(request.BehaviourPointsMultiplier),

                Teams = teams,
                Games = games,

                DisplayMode = request.DisplayMode,
                Hidden = request.Hidden,
                Paused = request.Paused,
                PausedAt = ToUtc(request.PausedAt),
                RevealStep = NormaliseRevealStep(request.RevealStep),

                UpdatedAt = updatedAt,
                ServiceId = service.Id
            };

            await db.GameBoards.AddAsync(board, token);
        }
        else
        {
            if (updatedAt < board.UpdatedAt)
                return new BasicReadResponse<Guid?>
                {
                    Entity = board.Id,
                    Success = true
                };

            board.CurrentGame = currentGame;
            board.StepPoints = NormalisePoints(request.StepPoints, fallback: 1);
            board.BonusPoints = NormalisePoints(request.BonusPoints, fallback: 5);
            board.PointsMultiplier = GameMultipliers.Normalise(request.PointsMultiplier);
            board.BehaviourPointsMultiplier = GameMultipliers.Normalise(request.BehaviourPointsMultiplier);
            board.Teams = teams;
            board.Games = games;
            board.DisplayMode = request.DisplayMode;
            board.Hidden = request.Hidden;
            board.Paused = request.Paused;
            board.PausedAt = ToUtc(request.PausedAt);
            board.RevealStep = NormaliseRevealStep(request.RevealStep);
            board.UpdatedAt = updatedAt;
        }

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = board.Id,
            Success = true
        };
    }

    /// <summary>
    /// Re-indexes the list so team indexes stay contiguous, and fills in a default name
    /// or colour for anything the client left blank or malformed.
    /// </summary>
    private static List<DbGameTeam> NormaliseTeams(IEnumerable<GameTeamDefinition> teams)
    {
        List<GameTeamDefinition> ordered = teams
            .OrderBy(x => x.Index)
            .Take(GameTeamDefaults.MaxTeams)
            .ToList();

        if (ordered.Count < GameTeamDefaults.MinTeams)
            ordered = [.. GameTeamDefaults.Default()];

        return ordered
            .Select((team, index) => new DbGameTeam
                {
                    Index = index,
                    Name = NormaliseName(team.Name, GameTeamDefaults.MaxNameLength)
                           ?? GameTeamDefaults.DefaultName(index),
                    Colour = GameTeamDefaults.IsValidColour(team.Colour)
                        ? team.Colour
                        : GameTeamDefaults.DefaultColour(index)
                }
            )
            .ToList();
    }

    /// <summary>
    /// Keeps only games that still say something - a name, teams combined, or a
    /// multiplier of their own - and drops alliance entries that point at teams the
    /// board no longer has.
    /// </summary>
    private static List<DbGame> NormaliseGames(IEnumerable<GameDefinition> games, int teamCount)
    {
        List<DbGame> normalised = [];

        foreach (GameDefinition game in games.OrderBy(x => x.Number))
        {
            if (game.Number < 1 || normalised.Any(x => x.Number == game.Number))
                continue;

            string? name = NormaliseName(game.Name, GameTeamDefaults.MaxGameNameLength);

            List<int> alliances = game.Alliances.Count == teamCount
                ? game.Alliances.Select(x => Math.Clamp(x, 0, teamCount - 1)).ToList()
                : [];

            // Every team in a group of its own is the same as no alliances at all.
            if (alliances.Distinct().Count() == alliances.Count)
                alliances = [];

            int? multiplier = GameMultipliers.Normalise(game.Multiplier);

            // Placement points are a game's whole way of being scored, so a game that has
            // them is worth keeping even when it is otherwise a plain numbered game.
            List<int>? placement = GamePlacements.Normalise(game.PlacementPoints)?.ToList();

            // A planned or hidden game is a decision in its own right, so it is kept even
            // when it holds nothing else at all.
            if (name == null
                && alliances.Count == 0
                && multiplier == null
                && placement == null
                && !game.Planned
                && !game.Hidden)
                continue;

            normalised.Add(new DbGame
                {
                    Number = game.Number,
                    Name = name,
                    Alliances = alliances,
                    Multiplier = multiplier,
                    PlacementPoints = placement,
                    Planned = game.Planned,
                    Hidden = game.Hidden
                }
            );
        }

        return normalised;
    }

    private static string? NormaliseName(string? name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string trimmed = name.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    /// <summary>
    /// A negative step is the same as no reveal - the display would clamp it to the start
    /// anyway, and storing it would leave a reveal that cannot be seen but is still "on".
    /// </summary>
    private static int? NormaliseRevealStep(int? step) => step is >= 0 ? step : null;

    private static int NormalisePoints(int points, int fallback) =>
        points is > 0 and <= 100 ? points : fallback;

    private static DateTimeOffset? ToUtc(DateTime? value) =>
        value == null || value == default(DateTime)
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
