using GSBC.ImpactKids.Grpc;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Interceptors;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.Attendance.AttendancePickupDisplayServices;
using GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceItemRecordServices;
using GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceItemTypeServices;
using GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceRecordServices;
using GSBC.ImpactKids.Grpc.Features.Authentication;
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

builder.Services.AddAuthentication()
    .AddJwtBearer("Bearer", jwtOptions =>
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
    });

builder.Services.AddAuthorization(opts =>
    {
        opts.AddPolicy(Policies.EnabledOnly, policy => policy.RequireClaim("Enabled", true.ToString()));
    }
);
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
builder.Services.AddTransient<ElvantoService>();
builder.Services.AddSingleton<EventingChannelsService>();
// Wakes the wall display's scoreboard stream - see GameDisplayService.WatchScoreboard.
builder.Services.AddSingleton<GameDataChangeNotifier>();
// Wakes the pickup wall's stream - see AttendancePickupDisplayService.WatchPickups.
builder.Services.AddSingleton<AttendanceDataChangeNotifier>();
builder.Services.AddHostedService<RabbitWorker>();
builder.Services.AddHostedService<HeartbeatService>();
builder.Services.AddHybridCache();

builder.Services.AddPooledDbContextFactory<GsbcDbContext>((sp, o) =>
{
    o.UseNpgsql(builder.Configuration.GetConnectionString("impact-kids"));
    o.AddInterceptors(sp.GetRequiredService<FieldChangeTrackingInterceptor>());
    // o.AddInterceptors(new GSBC.ImpactKids.Grpc.Data.Interceptors.LatencyInterceptor(TimeSpan.FromSeconds(1.5)));
});

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
app.MapGrpcService<PersonService>();
app.MapGrpcService<AllergyService>();
app.MapGrpcService<AllergenService>();
app.MapGrpcService<MedicalNoteService>();
app.MapGrpcService<MedicalTypeService>();
app.MapGrpcService<SchoolGradeService>();
app.MapGrpcService<ElvantoService>();
app.MapGrpcService<SchoolTermService>();
app.MapGrpcService<ServicesService>();
app.MapGrpcService<ServiceTypeService>();
app.MapGrpcService<DollarStoreEntryService>();
app.MapGrpcService<BibleService>();
app.MapGrpcService<MemoryVersesService>();
app.MapGrpcService<MemoryVerseListsService>();
app.MapGrpcService<MemorisationEntriesService>();
app.MapGrpcService<AttendanceRecordService>();
app.MapGrpcService<AttendanceItemTypeService>();
app.MapGrpcService<AttendanceItemRecordService>();
app.MapGrpcService<GamePointRecordService>();
app.MapGrpcService<GameBoardService>();
// Unauthenticated - wall display only, aggregate scores only.
app.MapGrpcService<GameDisplayService>();
// Unauthenticated - pickup wall only, first name plus last initial only.
app.MapGrpcService<AttendancePickupDisplayService>();
app.MapGrpcService<SyncService>();
app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.AddEventEndpoints();

using (IServiceScope scope = app.Services.CreateScope())
{
    IConnection          connection = scope.ServiceProvider.GetRequiredService<IConnection>();
    await using IChannel channel    = await connection.CreateChannelAsync();
    await channel.ExchangeDeclareAsync("data-events", ExchangeType.Topic);
    await channel.ExchangeDeclareAsync("events", ExchangeType.Fanout);
}

app.Run();