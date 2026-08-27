using Grpc.Net.Client.Web;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Authentication;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.DollarStore;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Elvanto;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture.Memorisation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GSBC.ImpactKids.WASM;
using GSBC.ImpactKids.WASM.Authentication;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Eventing.Services;
using GSBC.ImpactKids.WASM.Features.Games.Services;
using GSBC.ImpactKids.WASM.Services;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using ProtoBuf.Grpc.ClientFactory;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddStores();

// builder.Services.AddWhyDidYouRender(config =>
// {
//     config.Enabled = builder.HostEnvironment.IsDevelopment();
//     config.Verbosity = TrackingVerbosity.Normal;
//     config.Output = TrackingOutput.Both;
//     config.TrackParameterChanges = true;
//     config.EnableStateTracking = true;
// });

builder.Logging.AddConfiguration(
    builder.Configuration.GetSection("Logging"));

builder.AddServiceDefaults();
builder.Services.AddMudServices();
builder.Services.AddScoped<ExceptionInterceptor>();

// builder.Services.AddOidcAuthentication<RemoteAuthenticationState, RemoteUserAccount>(options =>
//     {
//         builder.Configuration.Bind("Auth0", options.ProviderOptions);
//         options.ProviderOptions.DefaultScopes.Add("offline_access");
//         options.ProviderOptions.ResponseType = "code";
//         options.ProviderOptions.AdditionalProviderParameters.Add("audience", "https://kids.baptist.com.au");
//     })
//     .AddAccountClaimsPrincipalFactory<RemoteAuthenticationState, RemoteUserAccount, CustomAccountFactory>();

builder.Services.AddAuthorizationCore(opts =>
    {
        opts.AddPolicy(Policies.EnabledOnly, policy => policy.RequireClaim("permissions", "user:enabled"));
    }
);
builder.Services.AddScoped<AuthenticationStateProvider, BffAuthenticationStateProvider>();
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) // used for /bff/user info
});
builder.Services.AddCascadingAuthenticationState();

builder.Services
    .AddCodeFirstGrpcClient<ILoginService>(
        typeof(ILoginService).FullName!,
        x => { x.Address = new Uri("https://yarp"); }
    )
    .ConfigureChannel(x => { x.UnsafeUseInsecureChannelCallCredentials = true; })
    .ConfigurePrimaryHttpMessageHandler(() =>
        new GrpcWebHandler(new HttpClientHandler())); // login service is unauthenticated

builder.Services.AddSingleton<ISseClientService, SseClientService>();

// Wall display client - unauthenticated on purpose, routed past the proxy's auth
// policy so a screen with no login can still read the scoreboard.
builder.Services
    .AddCodeFirstGrpcClient<IGameDisplayService>(
        typeof(IGameDisplayService).FullName!,
        x => { x.Address = new Uri("https://yarp"); }
    )
    .ConfigureChannel(x => { x.UnsafeUseInsecureChannelCallCredentials = true; })
    .ConfigurePrimaryHttpMessageHandler(() => new GrpcWebHandler(new HttpClientHandler()));

// builder.Services.AddScoped<UnauthorizedMessageHandler>();
builder.Services.AddAuthenticatedGrpcClient<IMetabaseService>();
builder.Services.AddAuthenticatedGrpcClient<IUsersService>();
builder.Services.AddAuthenticatedGrpcClient<IPersonService>();
builder.Services.AddAuthenticatedGrpcClient<IMedicalNoteService>();
builder.Services.AddAuthenticatedGrpcClient<IMedicalTypeService>();
builder.Services.AddAuthenticatedGrpcClient<IAllergyService>();
builder.Services.AddAuthenticatedGrpcClient<IAllergenService>();
builder.Services.AddAuthenticatedGrpcClient<ISchoolGradeService>();
builder.Services.AddAuthenticatedGrpcClient<IElvantoService>();
builder.Services.AddAuthenticatedGrpcClient<ISchoolTermsService>();
builder.Services.AddAuthenticatedGrpcClient<IServicesService>();
builder.Services.AddAuthenticatedGrpcClient<IServiceTypeService>();
builder.Services.AddAuthenticatedGrpcClient<IDollarStoreEntryService>();
builder.Services.AddAuthenticatedGrpcClient<IBibleService>();
builder.Services.AddAuthenticatedGrpcClient<IMemoryVersesService>();
builder.Services.AddAuthenticatedGrpcClient<IMemoryVersesServicesRelationshipService>();
builder.Services.AddAuthenticatedGrpcClient<IMemoryVersesBibleVersesRelationshipService>();
builder.Services.AddAuthenticatedGrpcClient<IMemoryVerseListsService>();
builder.Services.AddAuthenticatedGrpcClient<IMemorisationEntriesService>();
builder.Services.AddAuthenticatedGrpcClient<IAttendanceRecordService>();
builder.Services.AddAuthenticatedGrpcClient<IAttendanceItemTypeService>();
builder.Services.AddAuthenticatedGrpcClient<IAttendanceItemRecordService>();
builder.Services.AddAuthenticatedGrpcClient<IGamePointRecordService>();
builder.Services.AddAuthenticatedGrpcClient<IGameBoardService>();
builder.Services.AddAuthenticatedGrpcClient<ISyncService>();

// Offline first, so it outlives any page and keeps its outbox for the whole session.
builder.Services.AddSingleton<IGamePointsService, GamePointsService>();

MetabaseConfig? metabaseConfig = builder.Configuration.GetSection("metabase").Get<MetabaseConfig>();
if (metabaseConfig != null)
{
    builder.Services.AddSingleton(metabaseConfig);
}

ElvantoReportsConfig? reportsConfig = builder.Configuration.GetSection("elvanto").Get<ElvantoReportsConfig>();
if (reportsConfig != null)
{
    builder.Services.AddSingleton(reportsConfig);
}

WebAssemblyHost host = builder.Build();

// Start the global subscription before rendering
// ISseClientService sse = host.Services.GetRequiredService<ISseClientService>();
// await sse.StartAsync();

// Enable Why Did You Render
// IJSRuntime      jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
// await host.Services.InitializeWasmAsync(jsRuntime);

await host.RunAsync();