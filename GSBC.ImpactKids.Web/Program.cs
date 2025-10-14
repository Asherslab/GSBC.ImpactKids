using BitzArt.Blazor.Cookies;
using GSBC.ImpactKids.ServiceDefaults;
using GSBC.ImpactKids.Shared.Contracts.Services;
using GSBC.ImpactKids.Web.Components;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddRabbitMQClient("rabbitmq");
builder.AddRedisOutputCache("cache");

builder.Services.AddMudServices();
builder.AddBlazorCookies();

builder.AddAuthenticationServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpForwarderWithServiceDiscovery();
builder.Services.AddHttpContextAccessor();

// AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
builder.Services.AddAuthenticatedGrpcClient<IElvantoService>();
builder.Services.AddAuthenticatedGrpcClient<ISchoolTermsService>();
builder.Services.AddAuthenticatedGrpcClient<IServicesService>();
builder.Services.AddAuthenticatedGrpcClient<IBibleService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.MapGroup("/authentication").MapLoginAndLogout();

app.Run();