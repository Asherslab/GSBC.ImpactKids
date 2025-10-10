using System.Diagnostics;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Workers.DbMigrations;

public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource SActivitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        using var activity = SActivitySource.StartActivity("Migrating database", ActivityKind.Client);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<GsbcDbContext>();

            await RunMigrationAsync(dbContext, cancellationToken);
            await SeedDataAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private static async Task RunMigrationAsync(GsbcDbContext dbContext, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Run migration in a transaction to avoid partial migration if it fails.
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
    }

    private static async Task SeedDataAsync(GsbcDbContext dbContext, CancellationToken cancellationToken)
    {
        DbSchoolTerm schoolTerm = new()
        {
            Id = Guid.Empty,
            Name = "2025 Term 4",

            StartDate = DateTime.Parse("08/10/2025"),
            EndDate = DateTime.Parse("28/11/2025")
        };
        
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Seed the database
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            if (!await dbContext.Terms.AnyAsync(cancellationToken))
            {
                await dbContext.Terms.AddAsync(schoolTerm, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}