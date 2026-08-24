using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using GSBC.ImpactKids.YARP.DevAuth;
using GSBC.ImpactKids.YARP.Extensions;
using Microsoft.Extensions.Options;
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

        builder.MapGet("user", async (
            HttpContext              context,
            IUserTokenManager        tokenManager,
            IHostEnvironment         environment,
            IOptions<DevAuthOptions> devAuthOptions
        ) =>
        {
            if (!(context.User.Identity?.IsAuthenticated ?? false))
                return Results.Unauthorized();

            string? rawToken = null;

            if (DevAuthGate.IsOpen(environment, devAuthOptions))
                rawToken = context.User.FindFirst(DevAuthOptions.TokenClaimType)?.Value;

            if (rawToken == null)
            {
                TokenResult<UserToken> accessToken = await tokenManager.GetAccessTokenAsync(context.User);
                if (!accessToken.Succeeded)
                    return Results.Unauthorized();

                rawToken = accessToken.Token.AccessToken.ToString();
            }

            JwtSecurityToken?  jwt   = new JwtSecurityTokenHandler().ReadJwtToken(rawToken);
            IEnumerable<Claim> perms = jwt.Claims.Where(c => c.Type == "permissions");

            // The token itself is deliberately not among them - the browser has no use for
            // it, and it is already riding in the cookie.
            var claims = context.User.Claims
                .Where(c => c.Type != DevAuthOptions.TokenClaimType)
                .Select(c => new { c.Type, c.Value })
                .ToList();
            claims.AddRange(perms.Select(c => new { c.Type, c.Value }));
            return Results.Json(new { IsAuthenticated = true, Claims = claims });
        });

        return builder;
    }
}