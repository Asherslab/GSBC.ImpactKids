using System.Security.Claims;
using GSBC.ImpactKids.YARP.DisplayAuth;
using GSBC.ImpactKids.YARP.RequestTransformers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;

namespace GSBC.ImpactKids.YARP.Extensions;

internal static class HostExtensions
{
    /// <summary>
    /// Named on the <c>grpc</c> and <c>api</c> routes in <c>appsettings.json</c>. Admits both
    /// caller types; see where it is built for why that is not a widening.
    /// </summary>
    internal const string LeaderOrDisplayPolicy = "LeaderOrDisplay";

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

                    // Every route that requires a caller of any kind carries a token to the
                    // gRPC service. Which token depends on who the caller is, and the
                    // transform decides that per request - a leader's Auth0 token, or the
                    // display token the screen was handed at enrolment.
                    //
                    // This used to be the other way round for the display: the pickup route
                    // deliberately attached nothing, because the service behind it was
                    // anonymous. It is not anonymous any more, which is the whole point of
                    // the change - the gRPC service now authenticates a display itself
                    // rather than trusting this proxy to have done it.
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

                    // Everything behind the cookie policy is an API call from the SPA -
                    // gRPC, /api, /bff/user. The default 302 to LoginPath falls through
                    // to the wasm catch-all route, so the caller gets index.html with a
                    // 200, which grpc-web reports as
                    // "Bad gRPC response. Invalid content-type value: text/html".
                    // Answer with status codes and let the client drive /bff/login.
                    options.Events.OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    };
                })
                // A SECOND cookie scheme beside the leader session, never a widening of it.
                // It says one thing - "this screen enrolled on the current pickup display
                // key" - and the only route that names its policy is the pickup display
                // one. See DisplayAuthOptions for why that cannot reach anything else.
                .AddCookie(DisplayAuthOptions.SchemeName, options =>
                {
                    options.Cookie.Name = DisplayAuthOptions.CookieName;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.HttpOnly = true;

                    // Deliberately long, and deliberately not sliding on the ticket alone:
                    // the key is non-expiring by decision, rotation is how it ends, and a
                    // cookie that quietly expired would strand a TV mid service.
                    options.ExpireTimeSpan = DisplayAuthOptions.CookieLifetime;
                    options.SlidingExpiration = true;

                    // Same reasoning as the scheme above, and it matters more here: the
                    // caller is grpc-web on a wall display. A 302 falls through to the wasm
                    // catch-all and comes back as index.html with a 200, which grpc-web
                    // reports as "Bad gRPC response. Invalid content-type value: text/html"
                    // - and a wall with nobody standing at it just reads "Connecting...".
                    options.Events.OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    };

                    // Rotation has to be total, not merely forward looking. The cookie
                    // records which key the screen enrolled on; once a new one is minted
                    // every older cookie is rejected here and the wall says so.
                    options.Events.OnValidatePrincipal = async context =>
                    {
                        PickupDisplayKeyClient keys = context.HttpContext.RequestServices
                            .GetRequiredService<PickupDisplayKeyClient>();

                        Guid? current = await keys.CurrentGenerationAsync(context.HttpContext.RequestAborted);

                        string? enrolled = context.Principal?
                            .FindFirst(DisplayAuthOptions.GenerationClaimType)?.Value;

                        if (current != null && enrolled == current.Value.ToString())
                            return;

                        context.RejectPrincipal();

                        await context.HttpContext.SignOutAsync(DisplayAuthOptions.SchemeName);
                    };
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
                )
                // Names ONE scheme, and it is not the leader session's. A signed in leader
                // therefore does not satisfy this either - the wall is opened from its
                // setup link, by anybody or nobody, and that is the only way in.
                .AddPolicy(DisplayAuthOptions.PolicyName, policy => policy
                    .AddAuthenticationSchemes(DisplayAuthOptions.SchemeName)
                    .RequireAuthenticatedUser()
                )
                // What the gRPC and api routes require: a leader session OR an enrolled
                // screen. It says only "you are one of the two callers this app has" - which
                // of them may call WHAT is decided at the gRPC service, per method, and a
                // display reaches only the reads marked for it and can never write.
                //
                // Being permissive here is deliberate and is not the weakening it looks
                // like. Before this change the proxy was the ONLY thing standing between a
                // display and the data; now it proves enrolment and the service behind it
                // authenticates the display independently, so the gate that matters has
                // moved to where it can actually see what is being asked for.
                .AddPolicy(LeaderOrDisplayPolicy, policy => policy
                    .AddAuthenticationSchemes(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        DisplayAuthOptions.SchemeName
                    )
                    .RequireAuthenticatedUser()
                );

            return builder;
        }
    }
}