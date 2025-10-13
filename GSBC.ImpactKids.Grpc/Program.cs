using GSBC.ImpactKids.Grpc;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Services.SchoolTermServices;
using GSBC.ImpactKids.Grpc.Services.ServicesServices;
using GSBC.ImpactKids.ServiceDefaults;
using ProtoBuf.Grpc.Server;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRabbitMQClient("rabbitmq");

builder.Services.AddTransient(typeof(IEventService<>), typeof(EventService<>));

AuthConfig? config = builder.Configuration.GetSection("Google").Get<AuthConfig>();
builder.Services.AddAuthentication()
    .AddJwtBearer("Bearer", jwtOptions =>
    {
        jwtOptions.Authority = "https://accounts.google.com";
        jwtOptions.Audience = config?.ClientId;
    });

builder.Services.AddAuthorization();
builder.Services.AddCodeFirstGrpc();
builder.Services.AddGrpc();
builder.Services.AddConverters();

builder.AddNpgsqlDbContext<GsbcDbContext>("impact-kids");

ElvantoConfig? elvantoConfig = builder.Configuration.GetSection("Elvanto").Get<ElvantoConfig>();
if (elvantoConfig != null)
    builder.Services.AddSingleton(elvantoConfig);

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
app.MapGrpcService<ElvantoService>();
app.MapGrpcService<SchoolTermService>();
app.MapGrpcService<ServicesService>();
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