using System.Security.Claims;
using Grpc.Core;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Authentication.UsersServices;

public partial class UsersService
{
    public async Task<BasicResponse?> ToggleEnabled(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        string? userSub = context.ServerCallContext?.GetHttpContext().User
            .FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        
        if (userSub == null)
            return BasicResponse.WithError(PermissionDenied);
        
        DbUser? user = await db.Users
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (user == null)
            return BasicResponse.WithError(UserNotFound);
        
        if (user.GoogleSub == userSub)
            return BasicResponse.WithError(UserCannotToggleSelf);
        
        user.Enabled = !user.Enabled;
        
        db.Users.Update(user);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}