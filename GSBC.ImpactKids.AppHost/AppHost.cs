var builder = DistributedApplication.CreateBuilder(args);

builder.AddKubernetesEnvironment("k8s")
    .WithProperties(x =>
    {
        x.HelmChartName = "impact-kids-app";
        x.DefaultStorageType = "pvc";
    });

IResourceBuilder<RedisResource> cache = builder.AddRedis("cache")
    .WithLifetime(ContainerLifetime.Persistent);

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
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(db)
    .WithReference(migrations)
    .WaitForCompletion(migrations);

IResourceBuilder<ProjectResource> wasm = builder
    .AddStandaloneBlazorWebAssemblyProject<Projects.GSBC_ImpactKids_WASM>("wasm")
    .WithReference(grpcService)
    .WaitFor(grpcService);

grpcService.WithReference(wasm);

builder.Build().Run();