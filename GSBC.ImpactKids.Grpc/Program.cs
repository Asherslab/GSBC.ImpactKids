using GSBC.ImpactKids.Grpc;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Interceptors;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.Attendance.PickupDisplayKeyServices;
using GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceItemRecordServices;
using GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceItemTypeServices;
using GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceRecordServices;
using GSBC.ImpactKids.Grpc.Features.Authentication;
using GSBC.ImpactKids.Grpc.Features.Authentication.DisplayAuth;
using GSBC.ImpactKids.Grpc.Features.Authentication.UsersServices;
using GSBC.ImpactKids.Grpc.Features.DataDisplay;
using GSBC.ImpactKids.Grpc.Features.DollarStore.DollarStoreEntryServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.Eventing;
using GSBC.ImpactKids.Grpc.Features.Games.GameBoardServices;
using GSBC.ImpactKids.Grpc.Features.Games.GameDisplayServices;
using GSBC.ImpactKids.Grpc.Features.Games.GamePointRecordServices;
using GSBC.ImpactKids.Grpc.Features.Eventing.Services;
using GSBC.ImpactKids.Grpc.Features.People.AllergenServices;
using GSBC.ImpactKids.Grpc.Features.People.AllergyServices;
using GSBC.ImpactKids.Grpc.Features.People.MedicalNoteServices;
using GSBC.ImpactKids.Grpc.Features.People.MedicalTypeServices;
using GSBC.ImpactKids.Grpc.Features.People.PersonServices;
using GSBC.ImpactKids.Grpc.Features.People.Photos;
using Amazon.Runtime;
using Amazon.S3;
using GSBC.ImpactKids.Grpc.Features.Sync.SyncServices;
using GSBC.ImpactKids.Grpc.Features.People.SchoolGradeServices;
using GSBC.ImpactKids.Grpc.Features.Scheduling.School.SchoolTermServices;
using GSBC.ImpactKids.Grpc.Features.Scheduling.ServicesServices;
using GSBC.ImpactKids.Grpc.Features.Scheduling.ServiceTypeServices;
using GSBC.ImpactKids.Grpc.Features.Scripture.BibleServices;
using GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemorisationEntriesServices;
using GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVerseListsServices;
using GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVersesServices;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.ServiceDefaults;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ProtoBuf.Grpc.Server;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRabbitMQClient("rabbitmq");
builder.AddRedisDistributedCache("redis");

builder.Services.AddTransient(typeof(IEventService<>), typeof(EventService<>));

// The local sign in bypass mints its own tokens with a shared symmetric key instead of
// Auth0's. Three things have to line up before one is accepted, and the key is generated
// per run by the AppHost, so a token cannot outlive the process that issued it.
bool devAuthEnabled = builder.Environment.IsDevelopment() &&
                      builder.Configuration.GetValue<bool>("DevAuth:Enabled");
string? devAuthSigningKey = builder.Configuration["DevAuth:SigningKey"];
bool devAuthUsable = devAuthEnabled && (devAuthSigningKey?.Length ?? 0) >= 32;

// Holds the current display signing key in memory: JwtBearer resolves signing keys
// synchronously and this one lives behind a database read. Registered as both a singleton
// and a hosted service so the same instance does the refreshing and the answering.
builder.Services.AddSingleton<DisplaySigningKeyProvider>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DisplaySigningKeyProvider>());
builder.Services.AddHttpContextAccessor();

// The default scheme is pinned rather than inferred. With one scheme registered ASP.NET
// makes it the default automatically; the display scheme below is a second one, which
// silently removes that inference and would leave every leader-only endpoint with no scheme
// to authenticate against.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwtOptions =>
    {
        jwtOptions.Authority = $"https://{builder.Configuration["Auth0:Domain"]}";
        jwtOptions.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidAudience = builder.Configuration["Auth0:Audience"],
            ValidIssuer = $"https://{builder.Configuration["Auth0:Domain"]}"
        };

        if (!devAuthUsable)
            return;

        // Added to the Auth0 issuer and keys rather than replacing them, so a real token
        // still validates exactly as before and a local one is the only thing gained.
        jwtOptions.TokenValidationParameters.ValidIssuers =
            [jwtOptions.TokenValidationParameters.ValidIssuer!, "gsbc-dev-bypass"];
        jwtOptions.TokenValidationParameters.IssuerSigningKeys =
        [
            new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(devAuthSigningKey!)
            )
        ];
    })
    // A screen on a wall, not a person. Its token is minted by this service at enrolment and
    // signed with a key that lives on the enrolment key row in this service's own database,
    // so there is no shared secret to distribute and rotation invalidates every outstanding
    // token by replacing what verifies them.
    .AddJwtBearer(DisplayAuthDefaults.SchemeName);

// Configured separately from the scheme above because the key resolver needs a service, and
// the AddJwtBearer overload that takes a lambda has no way to reach one.
builder.Services.AddOptions<JwtBearerOptions>(DisplayAuthDefaults.SchemeName)
    .Configure<DisplaySigningKeyProvider>((displayOptions, signingKeys) =>
    {
        // Nothing here talks to an authority - this service issued the token itself.
        displayOptions.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidIssuer = DisplayAuthDefaults.Issuer,
            ValidAudience = DisplayAuthDefaults.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, _, _, _) => signingKeys.Resolve()
        };

        // A display token carries no subject, and this stops one being invented for it. The
        // inbound map turns "sub" into a nameidentifier claim, and CustomClaimsTransformation
        // creates a DbUser row for any nameidentifier it does not recognise - so a mapped
        // subject would manufacture a user row for every wall in the building.
        displayOptions.MapInboundClaims = false;
    });

builder.Services.AddAuthorization(opts => opts.AddGsbcPolicies());
builder.Services.AddTransient<ILogger>(p =>
{
    var loggerFactory = p.GetRequiredService<ILoggerFactory>();
    // You could also use the HttpContext to make the name dynamic for example
    return loggerFactory.CreateLogger("Static Logger");
});
builder.Services.AddTransient<IClaimsTransformation, CustomClaimsTransformation>();
builder.Services.AddCodeFirstGrpc();
builder.Services.AddGrpc();
builder.Services.AddConverters();
builder.Services.AddPeopleSync();
builder.Services.AddSingleton<FieldChangeTrackingInterceptor>();
// A display may read and may never write - enforced at the database rather than trusted to
// the policy attributes. See the class remarks.
builder.Services.AddSingleton<DisplayReadOnlyInterceptor>();
builder.Services.AddTransient<ElvantoService>();
builder.Services.AddSingleton<EventingChannelsService>();
// Wakes the wall display's scoreboard stream - see GameDisplayService.WatchScoreboard.
builder.Services.AddSingleton<GameDataChangeNotifier>();
builder.Services.AddHostedService<RabbitWorker>();
builder.Services.AddHostedService<HeartbeatService>();
builder.Services.AddHybridCache();

builder.Services.AddPooledDbContextFactory<GsbcDbContext>((sp, o) =>
{
    o.UseNpgsql(builder.Configuration.GetConnectionString("impact-kids"));
    o.AddInterceptors(sp.GetRequiredService<FieldChangeTrackingInterceptor>());
    o.AddInterceptors(sp.GetRequiredService<DisplayReadOnlyInterceptor>());
    // o.AddInterceptors(new GSBC.ImpactKids.Grpc.Data.Interceptors.LatencyInterceptor(TimeSpan.FromSeconds(1.5)));
});

// The photo object store. Absent configuration is a legitimate state - a deployment without a store
// simply has no photos, every face falls back to its coloured initial, and nothing else changes - so
// this registers nothing rather than failing to start.
PhotoStoreConfig? photoConfig = builder.Configuration
    .GetSection(PhotoStoreConfig.SectionName).Get<PhotoStoreConfig>();

if (photoConfig is not null && !string.IsNullOrWhiteSpace(photoConfig.ServiceUrl))
{
    builder.Services.AddSingleton(photoConfig);
    builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
        new BasicAWSCredentials(photoConfig.AccessKey, photoConfig.SecretKey),
        new AmazonS3Config
        {
            ServiceURL = photoConfig.ServiceUrl,
            // Neither SeaweedFS locally nor the in-cluster one has per-bucket DNS, so the bucket has
            // to travel in the path rather than the hostname.
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        }));
    builder.Services.AddScoped<PhotoStore>();
}

ElvantoConfig? elvantoConfig = builder.Configuration.GetSection("Elvanto").Get<ElvantoConfig>();
if (elvantoConfig != null)
{
    builder.Services.AddSingleton(elvantoConfig);
    // Singleton so the ceiling spans every sync run in this process, not one run at a time.
    builder.Services.AddSingleton(new ElvantoWriteBudget(elvantoConfig.MaxWrites));
}

// builder.Services.AddCors(options =>
// {
//     options.AddDefaultPolicy(policy =>
//     {
//         string[] clients = builder.Configuration.GetServiceEndpoints("wasm");
//
//         policy.WithOrigins(clients); // Add the clients as allowed origins for cross origin resource sharing.
//         policy.AllowAnyMethod()
//             .AllowAnyHeader()
//             .WithExposedHeaders("Grpc-Status", "Grpc-Message",
//                 "Grpc-Encoding", "Grpc-Accept-Encoding",
//                 "Grpc-Status-Details-Bin")
//             .AllowCredentials();
//         // policy.WithHeaders("X-Requested-With");
//     });
// });

var app = builder.Build();

if (devAuthUsable)
{
    app.Logger.LogWarning(
        "Dev auth bypass is ENABLED - locally signed tokens are accepted alongside Auth0. Development only");
}

// app.UseCors();
app.MapDefaultEndpoints();

app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
app.MapGrpcService<LoginService>();
app.MapGrpcService<MetabaseService>();
app.MapGrpcService<UsersService>();
app.MapGrpcService<PersonService>().AllowDisplay("BasicReadMultiple");
app.MapGrpcService<AllergyService>();
app.MapGrpcService<AllergenService>();
app.MapGrpcService<MedicalNoteService>();
app.MapGrpcService<MedicalTypeService>();
app.MapGrpcService<SchoolGradeService>();
app.MapGrpcService<ElvantoService>();
app.MapGrpcService<SchoolTermService>();
app.MapGrpcService<ServicesService>().AllowDisplay("BasicReadMultiple");
app.MapGrpcService<ServiceTypeService>();
app.MapGrpcService<DollarStoreEntryService>();
app.MapGrpcService<BibleService>();
app.MapGrpcService<MemoryVersesService>();
app.MapGrpcService<MemoryVerseListsService>();
app.MapGrpcService<MemorisationEntriesService>();
app.MapGrpcService<AttendanceRecordService>().AllowDisplay("BasicReadMultiple");
app.MapGrpcService<AttendanceItemTypeService>();
app.MapGrpcService<AttendanceItemRecordService>();
app.MapGrpcService<GamePointRecordService>();
app.MapGrpcService<GameBoardService>();
// Wall display only, aggregate scores only. No longer anonymous: a games wall enrols on the
// same display key as the pickup wall and presents the same token.
app.MapGrpcService<GameDisplayService>().AllowDisplay("GetScoreboard", "WatchScoreboard");
// The console that hands out a display's key, not a display itself. Leader only by falling
// back, like everything else here.
app.MapGrpcService<PickupDisplayKeyService>();
app.MapGrpcService<SyncService>();
// Anonymous, and explicitly so now that the fallback policy would otherwise close it. It is
// a signpost for somebody who opened the address in a browser and says nothing at all about
// this service's data.
app.MapGet("/",
        () =>
            "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909")
    .AllowAnonymous();

app.AddEventEndpoints();

// Cluster-internal only - the proxy asks these when a pickup wall enrols. Deliberately not
// routed in GSBC.ImpactKids.YARP/appsettings.json; see the class remarks.
app.AddPickupDisplayKeyEndpoints();

// Leader only by falling through to the EnabledOnly fallback policy, which is what keeps a wall
// display structurally unable to reach a child's face.
//
// Mapped only when a store is configured. A deployment without one is a legitimate state - no
// photos, every face falls back to its coloured initial - and leaving the routes unmapped makes
// that a 404, which is exactly what PersonAvatar already handles. Mapping them anyway would answer
// 500 instead, because the handlers resolve PhotoStore.
if (app.Services.GetService<PhotoStoreConfig>() is not null)
{
    app.AddPersonPhotoEndpoints();

    // The substitute for a photo sync: nothing can push a picture back through the Elvanto API, so
    // office staff get a zip to drag into Elvanto's own UI.
    app.AddPhotoExportEndpoints();

    using IServiceScope photoScope = app.Services.CreateScope();
    await photoScope.ServiceProvider.GetRequiredService<PhotoStore>().EnsureBucketAsync();
}

using (IServiceScope scope = app.Services.CreateScope())
{
    IConnection          connection = scope.ServiceProvider.GetRequiredService<IConnection>();
    await using IChannel channel    = await connection.CreateChannelAsync();
    await channel.ExchangeDeclareAsync("data-events", ExchangeType.Topic);
    await channel.ExchangeDeclareAsync("events", ExchangeType.Fanout);
}

app.Run();