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

// SeaweedFS, S3-compatible, for person photos. One bucket, reachable only from inside the cluster in
// production - the gRPC service is its only client, there is no ingress and no YARP route. Declared
// here so local dev has the same store to talk to. Not MinIO: the community edition was archived in
// early 2026, so it takes no security patches.
//
// The access key and secret are parameters so the secret is generated once and kept in the AppHost's
// user secrets, like sql-password and rabbitmq-password.
//
// This container is Persistent with a data volume, which is the shape that causes the failure in
// docs/modules/infrastructure/generated-passwords.md - but SeaweedFS does not have that failure.
// Postgres and RabbitMQ seal their password into the data directory on first init; SeaweedFS reads
// its S3 identity from AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY at every start and holds nothing
// about it on the volume. Verified 2026-08-29 by writing an object, restarting onto the same /data
// with a different secret, and reading the object back with the new credential while the old one was
// refused. The volume still holds real photos, so it is not a thing to delete casually - it just
// cannot lock you out.
IResourceBuilder<ParameterResource> s3AccessKey =
    builder.AddParameter("s3-access-key", "impact-kids", publishValueAsDefault: true);

// No special characters: this value is signed into S3 request headers and pasted into shell and YAML
// by hand often enough that a quoting mistake is the likelier failure than a short alphabet.
IResourceBuilder<ParameterResource> s3SecretKey = builder.AddResource(
    ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, "s3-secret-key", special: false));

IResourceBuilder<ContainerResource> s3 = builder.AddContainer("s3", "chrislusf/seaweedfs", "3.98")
    // The three volume flags are not tuning, they are load-bearing. Left at their defaults, `server`
    // allocates volume files of 1 GB each and grows them seven at a time, so three small objects took
    // 7 GB of disk - measured 2026-08-29, on a Docker VM it then filled. At 128 MB volumes with
    // preallocation off the same three objects take 236 KB. Ten years of photos is about 250 MB, so
    // eight 128 MB volumes is a comfortable ceiling.
    .WithArgs("server", "-dir=/data", "-s3", "-s3.port=8333",
        "-master.volumeSizeLimitMB=128", "-master.volumePreallocate=false", "-volume.max=8")
    .WithEnvironment("AWS_ACCESS_KEY_ID", s3AccessKey)
    .WithEnvironment("AWS_SECRET_ACCESS_KEY", s3SecretKey)
    .WithVolume("impact-kids-s3-data", "/data")
    .WithHttpEndpoint(port: 60537, targetPort: 8333, name: "s3")
    .WithLifetime(ContainerLifetime.Persistent);

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
    .WaitForCompletion(migrations)
    // The gRPC service is the object store's only client. Nothing else gets these, and there is no
    // ingress and no YARP route to the store - a photo reaches the browser through the API, under
    // the auth that already exists.
    .WithEnvironment("Photos__ServiceUrl", s3.GetEndpoint("s3"))
    .WithEnvironment("Photos__AccessKey", s3AccessKey)
    .WithEnvironment("Photos__SecretKey", s3SecretKey)
    .WaitFor(s3);

// A one-off, so it is declared with an explicit start rather than running on every stack start:
// it makes hundreds of outbound requests to Elvanto's CDN and there is nothing to gain from
// repeating it. Start it from the dashboard when a backfill is actually wanted.
builder.AddProject<Projects.GSBC_ImpactKids_Workers_PhotoBackfill>("photo-backfill")
    .WithReference(db)
    .WaitForCompletion(migrations)
    .WithEnvironment("Photos__ServiceUrl", s3.GetEndpoint("s3"))
    .WithEnvironment("Photos__AccessKey", s3AccessKey)
    .WithEnvironment("Photos__SecretKey", s3SecretKey)
    .WaitFor(s3)
    .WithExplicitStart();

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