using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Login;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services;

[AllowAnonymous]
public class LoginService(
    GsbcDbContext       db,
    IEventService<User> eventService
) : ILoginService
{
    public async Task<BasicReadResponse<bool>?> IsUserEnabled(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbUser? user = await db.Users.FirstOrDefaultAsync(x => x.GoogleSub == request.Id, token);

        if (user == null)
            return new BasicReadResponse<bool>
            {
                Success = false,
                Entity = false
            };

        return new BasicReadResponse<bool>
        {
            Success = true,
            Entity = user.Enabled
        };
    }

    public async Task<BasicResponse?> CreateSelf(CreateSelfRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbUser? user = await db.Users.FirstOrDefaultAsync(x => x.GoogleSub == request.GoogleSub, token);

        if (user != null)
            return BasicResponse.WithError(PermissionDenied);

        user = new DbUser
        {
            Id = Guid.Empty,
            GoogleSub = request.GoogleSub,
            Name = request.Name,

            Enabled = request.GoogleSub == "108820909534487863492" // sub claim for Asher
        };
        await db.Users.AddAsync(user, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(user.Id, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}