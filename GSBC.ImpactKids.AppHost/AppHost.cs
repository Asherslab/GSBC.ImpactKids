var builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<RedisResource> cache = builder.AddRedis("cache")
    .WithLifetime(ContainerLifetime.Persistent);

IResourceBuilder<RabbitMQServerResource> rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume()
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

IResourceBuilder<SqlServerServerResource> sql = builder.AddSqlServer("sql")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

IResourceBuilder<SqlServerDatabaseResource> db = sql.AddDatabase("impact-kids");

IResourceBuilder<ProjectResource> migrations =
    builder.AddProject<Projects.GSBC_ImpactKids_Workers_DbMigrations>("migrations")
        .WithReference(db)
        .WaitFor(db);

IResourceBuilder<ProjectResource> grpcService = builder.AddProject<Projects.GSBC_ImpactKids_Grpc>("grpc")
    .WithHttpHealthCheck("/health")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(db)
    .WithReference(migrations)
    .WaitForCompletion(migrations);

builder.AddProject<Projects.GSBC_ImpactKids_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(grpcService)
    .WaitFor(grpcService);

builder.Build().Run();