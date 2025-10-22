using System.Security.Claims;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authentication;

namespace GSBC.ImpactKids.Web.Services;

public class CustomClaimsTransformation(
    ILoginService loginService
) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ClaimsIdentity claimsIdentity = new();

        BasicReadResponse<bool>? userEnabled = await loginService.IsUserEnabled(new BasicReadRequest
            {
                Id = principal.FindFirstValue("sub") ?? ""
            }
        );

        if (userEnabled?.Success != true)
            return principal;

        string claimType = "Enabled";
        if (!principal.HasClaim(claim => claim.Type == claimType))
        {
            claimsIdentity.AddClaim(new Claim(claimType, userEnabled.Entity.ToString()));
        }

        principal.AddIdentity(claimsIdentity);
        return principal;
    }
}