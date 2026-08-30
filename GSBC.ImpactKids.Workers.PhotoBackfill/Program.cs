using Amazon.Runtime;
using Amazon.S3;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Photos;
using GSBC.ImpactKids.ServiceDefaults;
using GSBC.ImpactKids.Workers.PhotoBackfill;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddNpgsqlDbContext<GsbcDbContext>("impact-kids");

// Elvanto, read-only. The write gates stay off here whatever the configuration says elsewhere:
// this worker only ever GETs, and there is no way to write a picture through their API in any case.
ElvantoConfig elvantoConfig = builder.Configuration.GetSection("Elvanto").Get<ElvantoConfig>()
                              ?? throw new InvalidOperationException(
                                  "The photo backfill needs Elvanto configuration to read from.");

elvantoConfig.AllowWrites  = false;
elvantoConfig.AllowCreates = false;
elvantoConfig.AllowUpdates = false;

builder.Services.AddSingleton(elvantoConfig);
builder.Services.AddSingleton(new ElvantoWriteBudget(0));
builder.Services.AddHttpClient();
builder.Services.AddScoped<ElvantoService>();

PhotoStoreConfig photoConfig = builder.Configuration
                                   .GetSection(PhotoStoreConfig.SectionName).Get<PhotoStoreConfig>()
                               ?? throw new InvalidOperationException(
                                   "The photo backfill needs a Photos section naming the object store.");

builder.Services.AddSingleton(photoConfig);
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
    new BasicAWSCredentials(photoConfig.AccessKey, photoConfig.SecretKey),
    new AmazonS3Config
    {
        ServiceURL           = photoConfig.ServiceUrl,
        ForcePathStyle       = true,
        AuthenticationRegion = "us-east-1"
    }));
builder.Services.AddScoped<PhotoStore>();

IHost host = builder.Build();
host.Run();
