using Duende.AccessTokenManagement.OpenIdConnect;
using GSBC.ImpactKids.ServiceDefaults;
using GSBC.ImpactKids.YARP.Endpoints;
using GSBC.ImpactKids.YARP.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddReverseProxy();
builder.AddAuthenticationSchemes();

builder.AddRedisDistributedCache("redis");
builder.Services.AddOpenIdConnectAccessTokenManagement();

builder.Services.AddCors(options =>
{
    options.AddPolicy("allow-all", x =>
    {
        x.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("bff")
    .MapUserEndpoints();

app.MapDefaultEndpoints();

app.MapReverseProxy();

app.Run();