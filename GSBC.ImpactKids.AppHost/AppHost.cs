using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// User secrets hold the generated passwords for sql, redis, rabbitmq and cache. The host adds them
// only in Development, so a Production profile starts with no value for any of them, generates fresh
// ones, and persists those back over the originals.
//
// That is not a harmless rotation. The database container is Persistent with a data volume, and
// Postgres only applies POSTGRES_PASSWORD when it initialises an empty data directory - so the volume
// keeps the password it was built with while every caller now presents a new one. Running the PROD
// profile on 2026-08-24 did exactly this and locked the local database out with
// "28P01: password authentication failed for user postgres", with the original password gone.
//
// Load them regardless of environment so every profile resolves the same parameters. In Development
// this is a second, identical source and changes nothing.
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);

builder.AddKubernetesEnvironment("k8s")
    .WithProperties(x =>
    {
        x.HelmChartName = "impact-kids-app";
        x.DefaultStorageType = "pvc";
    });

IResourceBuilder<RedisResource> redis = builder.AddRedis("redis")
    .WithHostPort(60535)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithRedisInsight();

IResourceBuilder<RabbitMQServerResource> rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume()
    .WithManagementPlugin(63001)
    .WithLifetime(ContainerLifetime.Persistent);

IResourceBuilder<PostgresServerResource> sql = builder.AddPostgres("sql")
    .WithHostPort(60536)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

IResourceBuilder<PostgresDatabaseResource> db = sql.AddDatabase("impact-kids");

IResourceBuilder<ProjectResource> migrations =
    builder.AddProject<Projects.GSBC_ImpactKids_Workers_DbMigrations>("migrations")
        .WithReference(db)
        .WaitFor(db);

IResourceBuilder<ProjectResource> grpcService = builder.AddProject<Projects.GSBC_ImpactKids_Grpc>("grpc")
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(db)
    .WithReference(migrations)
    .WaitForCompletion(migrations);

IResourceBuilder<ProjectResource> wasm =
    builder.AddStandaloneBlazorWebAssemblyProject<Projects.GSBC_ImpactKids_WASM>("wasm");

IResourceBuilder<ProjectResource> yarp =
    builder.AddProject<Projects.GSBC_ImpactKids_YARP>("yarp");

yarp = yarp
    .WithReference(grpcService)
    .WaitFor(grpcService)
    .WithReference(wasm)
    .WaitFor(wasm)
    .WithExternalHttpEndpoints();

wasm = wasm.WithReference(yarp);

grpcService.WithReference(wasm);

// Local sign in without Auth0, for driving the app on a laptop. Run mode only, so it can
// never be baked into a published manifest or helm chart, and Development only. The key is
// fresh every run and only ever lives in these two processes' environment - nothing to
// commit, and yesterday's token is already dead.
if (builder.ExecutionContext.IsRunMode && builder.Environment.IsDevelopment())
{
    string devAuthSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    foreach (IResourceBuilder<ProjectResource> project in new[] { grpcService, yarp })
    {
        project
            .WithEnvironment("DevAuth__Enabled", "true")
            .WithEnvironment("DevAuth__SigningKey", devAuthSigningKey);
    }
}

builder.Build().Run();