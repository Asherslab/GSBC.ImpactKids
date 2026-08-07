using GSBC.ImpactKids.Grpc.Data.Models.Games;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
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

        DbGameBoard? board = await db.GameBoards
            .FirstOrDefaultAsync(x => x.ServiceId == service.Id, token);

        if (board == null)
        {
            board = new DbGameBoard
            {
                Id = Guid.Empty,

                CurrentGame = Math.Max(1, request.CurrentGame),
                TeamCount = NormaliseTeamCount(request.TeamCount),
                StepPoints = NormalisePoints(request.StepPoints, fallback: 1),
                BonusPoints = NormalisePoints(request.BonusPoints, fallback: 5),

                DisplayMode = request.DisplayMode,
                Hidden = request.Hidden,
                Paused = request.Paused,
                PausedAt = ToUtc(request.PausedAt),

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

            board.CurrentGame = Math.Max(1, request.CurrentGame);
            board.TeamCount = NormaliseTeamCount(request.TeamCount);
            board.StepPoints = NormalisePoints(request.StepPoints, fallback: 1);
            board.BonusPoints = NormalisePoints(request.BonusPoints, fallback: 5);
            board.DisplayMode = request.DisplayMode;
            board.Hidden = request.Hidden;
            board.Paused = request.Paused;
            board.PausedAt = ToUtc(request.PausedAt);
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

    private static int NormaliseTeamCount(int teamCount) => teamCount is 2 or 4 ? teamCount : 4;

    private static int NormalisePoints(int points, int fallback) =>
        points is > 0 and <= 100 ? points : fallback;

    private static DateTimeOffset? ToUtc(DateTime? value) =>
        value == null || value == default(DateTime)
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
