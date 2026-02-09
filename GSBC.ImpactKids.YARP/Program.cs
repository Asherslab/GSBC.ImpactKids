using Duende.AccessTokenManagement.OpenIdConnect;
using GSBC.ImpactKids.ServiceDefaults;
using GSBC.ImpactKids.YARP.Endpoints;
using GSBC.ImpactKids.YARP.Extensions;

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

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("bff")
    .MapUserEndpoints();

app.MapDefaultEndpoints();

app.MapReverseProxy();

app.Run();