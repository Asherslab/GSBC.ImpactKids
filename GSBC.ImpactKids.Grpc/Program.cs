using GSBC.ImpactKids.Grpc;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Grpc.Services.BibleServices;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Services.EventServices;
using GSBC.ImpactKids.Grpc.Services.EventServices.Internal;
using GSBC.ImpactKids.Grpc.Services.MemorisationEntriesServices;
using GSBC.ImpactKids.Grpc.Services.MemoryVerseListsServices;
using GSBC.ImpactKids.Grpc.Services.MemoryVersesServices;
using GSBC.ImpactKids.Grpc.Services.PeopleServices;
using GSBC.ImpactKids.Grpc.Services.SchoolTermServices;
using GSBC.ImpactKids.Grpc.Services.ServicesServices;
using GSBC.ImpactKids.Grpc.Services.UsersServices;
using GSBC.ImpactKids.ServiceDefaults;
using Microsoft.AspNetCore.Authentication;
using ProtoBuf.Grpc.Server;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRabbitMQClient("rabbitmq");

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
builder.Services.AddSingleton<EventServicesService>();
builder.Services.AddTransient<KeyedEventService>();

builder.AddNpgsqlDbContext<GsbcDbContext>("impact-kids");

ElvantoConfig? elvantoConfig = builder.Configuration.GetSection("Elvanto").Get<ElvantoConfig>();
if (elvantoConfig != null)
    builder.Services.AddSingleton(elvantoConfig);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        string[] clients = builder.Configuration.GetServiceEndpoints("wasm");

        policy.WithOrigins(clients); // Add the clients as allowed origins for cross origin resource sharing.
        policy.AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("Grpc-Status", "Grpc-Message",
                "Grpc-Encoding", "Grpc-Accept-Encoding",
                "Grpc-Status-Details-Bin")
            .AllowCredentials();
        // policy.WithHeaders("X-Requested-With");
    });
});

var app = builder.Build();

app.UseCors();
app.MapDefaultEndpoints();

app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
app.MapGrpcService<LoginService>();
app.MapGrpcService<EventService>();
app.MapGrpcService<MetabaseService>();
app.MapGrpcService<UsersService>();
app.MapGrpcService<PeopleService>();
app.MapGrpcService<ElvantoService>();
app.MapGrpcService<SchoolTermService>();
app.MapGrpcService<ServicesService>();
app.MapGrpcService<BibleService>();
app.MapGrpcService<MemoryVersesService>();
app.MapGrpcService<MemoryVerseListsService>();
app.MapGrpcService<MemorisationEntriesService>();
app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

using (IServiceScope scope = app.Services.CreateScope())
{
    IConnection          connection = scope.ServiceProvider.GetRequiredService<IConnection>();
    await using IChannel channel    = await connection.CreateChannelAsync();
    await channel.ExchangeDeclareAsync("data-events", ExchangeType.Topic);
}

app.Run();