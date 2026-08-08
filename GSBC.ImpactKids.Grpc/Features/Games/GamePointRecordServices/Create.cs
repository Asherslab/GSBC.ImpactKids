using System.Security.Claims;
using Grpc.Core;
using GSBC.ImpactKids.Grpc.Data.Models.Games;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Games.GamePointRecordServices;

public partial class GamePointRecordService
{
    public async Task<BasicReadResponse<Guid?>> Create(
        CreateGamePointRecordRequest request,
        CallContext                  context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        if (request.Id == Guid.Empty)
            return BasicReadResponse<Guid?>.WithError(GamePointRecordIdRequired);

        if (request.Points == 0)
            return BasicReadResponse<Guid?>.WithError(GamePointRecordPointsZero);

        if (request.TeamIndex < 0 || request.TeamIndex >= GameTeamDefaults.MaxTeams)
            return BasicReadResponse<Guid?>.WithError(GamePointRecordTeamIndex);

        DbService? service = await db.Services.FirstOrDefaultAsync(x => x.Id == request.ServiceId, token);

        if (service == null)
            return BasicReadResponse<Guid?>.WithError(ServiceNotFound);

        string? userId = context.ServerCallContext?.GetHttpContext().User
            .FindFirstValue("UserId");

        if (userId == null)
            return BasicReadResponse<Guid?>.WithError(PermissionDenied);

        // The client owns the id, so a retried send of a record we already stored
        // must succeed without adding the points a second time.
        bool alreadyStored = await db.GamePointRecords.AnyAsync(x => x.Id == request.Id, token);

        if (alreadyStored)
            return new BasicReadResponse<Guid?>
            {
                Entity = request.Id,
                Success = true
            };

        DbGamePointRecord record = new()
        {
            Id = request.Id,

            TeamIndex = request.TeamIndex,
            Points = request.Points,
            GameNumber = request.GameNumber is > 0 ? request.GameNumber : null,
            GroupId = request.GroupId == Guid.Empty ? null : request.GroupId,
            Awarded = request.Awarded == default
                ? DateTimeOffset.UtcNow
                : new DateTimeOffset(DateTime.SpecifyKind(request.Awarded, DateTimeKind.Utc)),

            ServiceId = service.Id,
            AwardedUserId = Guid.Parse(userId)
        };

        await db.GamePointRecords.AddAsync(record, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = record.Id,
            Success = true
        };
    }
}
