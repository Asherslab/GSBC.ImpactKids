using GSBC.ImpactKids.Grpc;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Grpc.Features.Authentication;
using GSBC.ImpactKids.Grpc.Features.Authentication.UsersServices;
using GSBC.ImpactKids.Grpc.Features.DataDisplay;
using GSBC.ImpactKids.Grpc.Features.DollarStore.DollarStoreEntryServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.Eventing;
using GSBC.ImpactKids.Grpc.Features.Eventing.Api.EventingServices;
using GSBC.ImpactKids.Grpc.Features.Eventing.Services;
using GSBC.ImpactKids.Grpc.Features.People.AllergenServices;
using GSBC.ImpactKids.Grpc.Features.People.AllergyServices;
using GSBC.ImpactKids.Grpc.Features.People.MedicalNoteServices;
using GSBC.ImpactKids.Grpc.Features.People.MedicalTypeServices;
using GSBC.ImpactKids.Grpc.Features.People.PersonServices;
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

builder.Services.AddAuthentication()
    .AddJwtBearer("Bearer", jwtOptions =>
    {
        jwtOptions.Authority = $"https://{builder.Configuration["Auth0:Domain"]}";
        jwtOptions.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidAudience = builder.Configuration["Auth0:Audience"],
            ValidIssuer = $"https://{builder.Configuration["Auth0:Domain"]}"
        };
    });

builder.Services.AddAuthorization(opts =>
    {
        opts.AddPolicy(Policies.EnabledOnly, policy => policy.RequireClaim("Enabled", true.ToString()));
    }
);
builder.Services.AddTransient<IClaimsTransformation, CustomClaimsTransformation>();
builder.Services.AddCodeFirstGrpc();
builder.Services.AddGrpc();
builder.Services.AddConverters();
builder.Services.AddTransient<ElvantoService>();
builder.Services.AddSingleton<EventingChannelsService>();
builder.Services.AddHostedService<RabbitWorker>();
builder.Services.AddHybridCache();

builder.Services.AddPooledDbContextFactory<GsbcDbContext>(o =>
{
    o.UseNpgsql(builder.Configuration.GetConnectionString("impact-kids"));
    // o.AddInterceptors(new GSBC.ImpactKids.Grpc.Data.Interceptors.LatencyInterceptor(TimeSpan.FromSeconds(1.5)));
});

ElvantoConfig? elvantoConfig = builder.Configuration.GetSection("Elvanto").Get<ElvantoConfig>();
if (elvantoConfig != null)
    builder.Services.AddSingleton(elvantoConfig);

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

// app.UseCors();
app.MapDefaultEndpoints();

app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
app.MapGrpcService<LoginService>();
app.MapGrpcService<EventingService>();
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