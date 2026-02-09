using System.Security.Claims;
using GSBC.ImpactKids.YARP.RequestTransformers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;

namespace GSBC.ImpactKids.YARP.Extensions;

internal static class HostExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddReverseProxy()
        {
            builder.Services.AddSingleton<AddBearerTokenToHeadersTransform>();
            // builder.Services.AddSingleton<AddAntiforgeryTokenResponseTransform>();
            // builder.Services.AddSingleton<ValidateAntiforgeryTokenRequestTransform>();

            builder.Services
                .AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
                .AddTransforms(builderContext =>
                {
                    // builderContext.ResponseTransforms.Add(builderContext.Services.GetRequiredService<AddAntiforgeryTokenResponseTransform>());
                    // builderContext.RequestTransforms.Add(builderContext.Services.GetRequiredService<ValidateAntiforgeryTokenRequestTransform>());
                    builderContext.RequestTransforms.Add(new RequestHeaderRemoveTransform("Cookie"));

                    if (!string.IsNullOrEmpty(builderContext.Route.AuthorizationPolicy))
                    {
                        builderContext.RequestTransforms.Add(builderContext.Services
                            .GetRequiredService<AddBearerTokenToHeadersTransform>());
                    }
                })
                .AddServiceDiscoveryDestinationResolver();

            return builder;
        }

        public IHostApplicationBuilder AddAuthenticationSchemes()
        {
            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.Name = "__gsbc_yarp";
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.HttpOnly = true;
                })
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, configureOptions: options =>
                {
                    options.Authority = builder.Configuration.GetValue<string>("OpenIDConnectSettings:Authority");
                    options.ClientId = builder.Configuration.GetValue<string>("OpenIDConnectSettings:ClientId");
                    options.ClientSecret = builder.Configuration.GetValue<string>("OpenIDConnectSettings:ClientSecret");

                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.ResponseMode = OpenIdConnectResponseMode.Query;

                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.SaveTokens = true;
                    options.MapInboundClaims = false;
                    options.CallbackPath = "/bff/signin-oidc";

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = ClaimTypes.NameIdentifier,
                        RoleClaimType = ClaimTypes.Role,
                    };

                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("permissions");
                    options.Scope.Add("offline_access");

                    options.Events = new OpenIdConnectEvents
                    {
                        OnRedirectToIdentityProviderForSignOut = (context) =>
                        {
                            string logoutUri =
                                $"{builder.Configuration.GetValue<string>("OpenIDConnectSettings:Authority")}/oidc/logout?client_id={builder.Configuration.GetValue<string>("OpenIDConnectSettings:ClientId")}";
                            string redirectUrl = context.HttpContext.BuildRedirectUrl(context.Properties.RedirectUri);
                            logoutUri += $"&post_logout_redirect_uri={redirectUrl}";

                            context.Response.Redirect(logoutUri);
                            context.HandleResponse();
                            return Task.CompletedTask;
                        },
                        OnRedirectToIdentityProvider = (context) =>
                        {
                            // Auth0 specific parameter to specify the audience
                            context.ProtocolMessage.SetParameter("audience",
                                builder.Configuration.GetValue<string>("OpenIDConnectSettings:Audience"));
                            return Task.CompletedTask;
                        },
                    };
                });

            builder.Services
                .AddAuthorizationBuilder()
                .SetDefaultPolicy(new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build()
                );

            return builder;
        }
    }
}