using Duende.AccessTokenManagement.OpenIdConnect;
using GSBC.ImpactKids.ServiceDefaults;
using GSBC.ImpactKids.YARP.DevAuth;
using GSBC.ImpactKids.YARP.Endpoints;
using GSBC.ImpactKids.YARP.Extensions;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddReverseProxy();
builder.AddAuthenticationSchemes();

builder.Services.AddOpenIdConnectAccessTokenManagement();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddProblemDetails();
builder.Services.Configure<DevAuthOptions>(builder.Configuration.GetSection(DevAuthOptions.SectionName));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

RouteGroupBuilder bff = app.MapGroup("bff");
bff.MapUserEndpoints();

// Local sign in, skipping Auth0. Development only, opt in, and needs a signing key the
// AppHost generates per run - see DevAuthOptions. The routes simply do not exist otherwise.
if (DevAuthGate.IsOpen(app.Environment, app.Services.GetRequiredService<IOptions<DevAuthOptions>>()))
{
    bff.MapDevAuthEndpoints();
    app.Logger.LogWarning(
        "Dev auth bypass is ENABLED. /bff/dev-login will hand out a session without Auth0. Development only");
}

app.MapDefaultEndpoints();

app.MapReverseProxy();

app.Run();