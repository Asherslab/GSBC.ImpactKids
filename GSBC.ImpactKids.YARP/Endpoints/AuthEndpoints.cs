using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using GSBC.ImpactKids.YARP.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace GSBC.ImpactKids.YARP.Endpoints;

internal static class AuthEndpoints
{
    internal static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("login", (string? returnUrl, HttpContext context) =>
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = context.BuildRedirectUrl(returnUrl),
            };

            return TypedResults.Challenge(properties);
        });

        builder.MapGet("logout", (string? redirectUrl, HttpContext context) =>
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = context.BuildRedirectUrl(redirectUrl),
            };

            return TypedResults.SignOut(properties,
                [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
        });

        builder.MapGet("user", async (HttpContext context, IUserTokenManager tokenManager) =>
        {
            if (!(context.User.Identity?.IsAuthenticated ?? false))
                return Results.Unauthorized();

            TokenResult<UserToken> accessToken = await tokenManager.GetAccessTokenAsync(context.User);
            if (!accessToken.Succeeded)
                return Results.Unauthorized();

            JwtSecurityToken?  jwt   = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Token.AccessToken);
            IEnumerable<Claim> perms = jwt.Claims.Where(c => c.Type == "permissions");

            var claims = context.User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            claims.AddRange(perms.Select(c => new { c.Type, c.Value }));
            return Results.Json(new { IsAuthenticated = true, Claims = claims });
        });

        return builder;
    }
}