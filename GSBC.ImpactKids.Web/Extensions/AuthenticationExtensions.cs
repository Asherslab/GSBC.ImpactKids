using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace GSBC.ImpactKids.Web.Extensions;

public static class AuthenticationExtensions
{
    public const string OidcScheme = "Google";
    
    public static void AddAuthenticationServices(this WebApplicationBuilder builder)
    {
        GoogleConfig? googleConfig = builder.Configuration.GetSection("Google").Get<GoogleConfig>();
        builder.Services.AddAuthentication(OidcScheme)
            .AddOpenIdConnect(OidcScheme, oidcOptions =>
            {
                oidcOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                oidcOptions.Authority = "https://accounts.google.com";
                oidcOptions.ClientId = googleConfig?.ClientId;
                oidcOptions.ResponseType = OpenIdConnectResponseType.Code;
                oidcOptions.MapInboundClaims = false;
                oidcOptions.TokenValidationParameters.NameClaimType = "name";
                oidcOptions.TokenValidationParameters.RoleClaimType = "roles";
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);
        
        
        builder.Services.ConfigureCookieOidc(CookieAuthenticationDefaults.AuthenticationScheme, OidcScheme);

        builder.Services.AddAuthorization();

        builder.Services.AddCascadingAuthenticationState();
    }
}

public class GoogleConfig
{
    public required string ClientId { get; set; }
}