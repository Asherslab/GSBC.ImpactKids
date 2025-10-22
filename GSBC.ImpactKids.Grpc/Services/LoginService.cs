using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services;

public class LoginService(
    GsbcDbContext       db,
    IEventService<User> eventService
) : ILoginService
{
    [AllowAnonymous]
    public async Task<BasicReadResponse<bool>?> IsUserEnabled(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbUser? user = await db.Users.FirstOrDefaultAsync(x => x.GoogleSub == request.Id, token);

        if (user == null)
            return new BasicReadResponse<bool>
            {
                Success = true,
                Entity = false
            };

        return new BasicReadResponse<bool>
        {
            Success = true,
            Entity = user.Enabled
        };
    }
}