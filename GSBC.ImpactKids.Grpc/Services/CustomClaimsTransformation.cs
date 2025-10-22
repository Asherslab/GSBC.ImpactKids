using System.Security.Claims;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services;

public class CustomClaimsTransformation(
    GsbcDbContext db
) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ClaimsIdentity claimsIdentity = new();

        string? subClaim = principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (subClaim == null)
            return principal;

        DbUser? user = await db.Users.FirstOrDefaultAsync(x => x.GoogleSub == subClaim);

        if (user == null)
        {
            user = new DbUser
            {
                Id = Guid.Empty,
                GoogleSub = subClaim,
                Name = principal.FindFirstValue("name") ?? "Name Not Found",

                Enabled = subClaim == "108820909534487863492" // sub claim for Asher
            };
            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();
        }
        
        const string claimType = "Enabled";
        if (!principal.HasClaim(claim => claim.Type == claimType))
        {
            claimsIdentity.AddClaim(new Claim(claimType, user.Enabled.ToString()));
        }

        principal.AddIdentity(claimsIdentity);
        return principal;
    }
}