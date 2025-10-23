using GSBC.ImpactKids.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace GSBC.ImpactKids.Web.Extensions;

internal static class CookieOidcServiceCollectionExtensions
{
    public static IServiceCollection ConfigureCookieOidc(
        this IServiceCollection services,
        string                  cookieScheme,
        string                  oidcScheme
    )
    {
        services.AddSingleton<CookieOidcRefresher>();

        services.AddOptions<CookieAuthenticationOptions>(cookieScheme)
            .Configure<CookieOidcRefresher>((cookieOptions, refresher) =>
            {
                cookieOptions.Events.OnValidatePrincipal = context =>
                    refresher.ValidateOrRefreshCookieAsync(context, oidcScheme);
            });

        services.AddOptions<OpenIdConnectOptions>(oidcScheme).Configure(oidcOptions =>
        {
            // Request a refresh_token.
            oidcOptions.Events.OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.SetParameter("access_type", "offline");
                // context.ProtocolMessage.SetParameter("prompt", "consent"); // required to actually receive a refresh_token
                // but refresh tokens are still really weird and buggy sadly. easier to not request it
                return Task.CompletedTask;
            };
            // Store the refresh_token.
            oidcOptions.SaveTokens = true;
        });

        return services;
    }
}
