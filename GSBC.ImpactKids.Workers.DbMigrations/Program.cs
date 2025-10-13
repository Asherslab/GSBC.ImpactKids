using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.ServiceDefaults;
using GSBC.ImpactKids.Workers.DbMigrations;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddNpgsqlDbContext<GsbcDbContext>("impact-kids");

var host = builder.Build();
host.Run();