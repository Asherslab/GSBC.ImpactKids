using System.Security.Claims;
using Grpc.Core;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services;

public class LoginService(
    GsbcDbContext       db
) : ILoginService
{
    [Authorize]
    public async Task<BasicReadResponse<bool>?> IsUserEnabled(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        ClaimsPrincipal principal = context.ServerCallContext!.GetHttpContext().User;
        string? sub = principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        DbUser? user = await db.Users.FirstOrDefaultAsync(x => x.GoogleSub == sub, token);

        if (user == null) // should be created by CustomClaimsTransformer already
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
}