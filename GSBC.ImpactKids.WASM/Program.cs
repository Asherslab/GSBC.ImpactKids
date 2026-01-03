using Grpc.Net.Client.Web;
using GSBC.ImpactKids.Shared.Contracts.Services;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.DollarStore;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Eventing;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture.Memorisation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GSBC.ImpactKids.WASM;
using GSBC.ImpactKids.WASM.Authentication;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Eventing.Services;
using GSBC.ImpactKids.WASM.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
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
builder.Services.AddSingleton<EventSubscriptionService>();
builder.Services.AddScoped<ExceptionInterceptor>();

builder.Services.AddOidcAuthentication<RemoteAuthenticationState, RemoteUserAccount>(options =>
    {
        builder.Configuration.Bind("Auth0", options.ProviderOptions);
        options.ProviderOptions.ResponseType = "code";
        options.ProviderOptions.AdditionalProviderParameters.Add("audience", builder.Configuration["Auth0:Audience"]!);
    })
    .AddAccountClaimsPrincipalFactory<RemoteAuthenticationState, RemoteUserAccount, CustomAccountFactory>();

builder.Services.AddAuthorizationCore(opts =>
    {
        opts.AddPolicy(Policies.EnabledOnly, policy => policy.RequireClaim("Enabled", true.ToString()));
    }
);
builder.Services.AddCascadingAuthenticationState();

builder.Services
    .AddCodeFirstGrpcClient<ILoginService>(
        typeof(ILoginService).FullName!,
        x => { x.Address = new Uri("https://grpc"); }
    )
    .ConfigureChannel(x => { x.UnsafeUseInsecureChannelCallCredentials = true; })
    .ConfigurePrimaryHttpMessageHandler(() =>
        new GrpcWebHandler(new HttpClientHandler())); // login service is unauthenticated

builder.Services.AddSingleton<ISseClientService, SseClientService>();

builder.Services.AddScoped<UnauthorizedMessageHandler>();
builder.Services.AddAuthenticatedGrpcClient<IEventingService>();
builder.Services.AddAuthenticatedGrpcClient<IEventService>();
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
builder.Services.AddAuthenticatedGrpcClient<IMemoryVerseListsService>();
builder.Services.AddAuthenticatedGrpcClient<IMemorisationEntriesService>();

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