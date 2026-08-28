using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GSBC.ImpactKids.YARP.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GSBC.ImpactKids.YARP.DevAuth;

internal static class DevAuthEndpoints
{
    /// <summary>
    /// Mapped only when <see cref="DevAuthGate"/> is open, so in any other environment these
    /// routes do not exist at all - a 404 rather than a disabled handler waiting to be
    /// re-enabled by a stray config value.
    /// </summary>
    internal static IEndpointRouteBuilder MapDevAuthEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("dev-login", async (
            string?                   returnUrl,
            HttpContext               context,
            IOptions<DevAuthOptions>  options,
            IHostEnvironment          environment,
            ILoggerFactory            loggerFactory
        ) =>
        {
            // Checked again here rather than trusting the registration alone - this endpoint
            // hands out a session, so it verifies its own preconditions.
            if (!DevAuthGate.IsOpen(environment, options))
                return Results.NotFound();

            DevAuthOptions dev = options.Value;

            string token = MintAccessToken(dev, context);

            ClaimsIdentity identity = new(
                [
                    new Claim(ClaimTypes.NameIdentifier, dev.Subject),
                    new Claim("name", dev.Name),
                    new Claim(DevAuthOptions.TokenClaimType, token)
                ],
                // Deliberately the OIDC scheme, matching what a real Auth0 sign in leaves in
                // the cookie - the identity is minted by the OpenIdConnect handler there, not
                // by the cookie handler, so its authentication type is NOT "Cookies".
                //
                // This used to say CookieAuthenticationDefaults.AuthenticationScheme, and that
                // divergence hid a production bug: code that asked "is this a leader?" by
                // comparing the authentication type passed locally and failed against Auth0.
                // Keep the bypass shaped like the real thing, so that mistake fails here too.
                OpenIdConnectDefaults.AuthenticationScheme,
                nameType: ClaimTypes.NameIdentifier,
                roleType: ClaimTypes.Role
            );

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = DateTimeOffset.UtcNow.Add(dev.Lifetime)
                }
            );

            loggerFactory
                .CreateLogger(typeof(DevAuthEndpoints))
                .LogWarning("Dev auth bypass issued a session for {Subject}. Development only", dev.Subject);

            return Results.Redirect(context.BuildRedirectUrl(returnUrl));
        });

        // The real /bff/logout goes out to Auth0 to end a session that was never started
        // there, and comes back an error. This one just drops the cookie.
        builder.MapGet("dev-logout", async (
            string?                  returnUrl,
            HttpContext              context,
            IOptions<DevAuthOptions> options,
            IHostEnvironment         environment
        ) =>
        {
            if (!DevAuthGate.IsOpen(environment, options))
                return Results.NotFound();

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return Results.Redirect(context.BuildRedirectUrl(returnUrl));
        });

        return builder;
    }

    /// <summary>
    /// A token shaped like the Auth0 one the gRPC service normally sees: same audience, the
    /// <c>sub</c> its claims transformation looks the user up by, and the
    /// <c>permissions</c> the client's own policy checks. Signed with the shared local key
    /// instead of Auth0's, which is the only difference and the only thing gating it.
    /// </summary>
    private static string MintAccessToken(DevAuthOptions dev, HttpContext context)
    {
        string? audience = context.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetValue<string>("OpenIDConnectSettings:Audience");

        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(dev.SigningKey!)),
            SecurityAlgorithms.HmacSha256
        );

        JwtSecurityToken token = new(
            issuer: DevAuthOptions.Issuer,
            audience: audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, dev.Subject),
                new Claim("name", dev.Name),
                new Claim("permissions", "user:enabled")
            ],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(dev.Lifetime),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
