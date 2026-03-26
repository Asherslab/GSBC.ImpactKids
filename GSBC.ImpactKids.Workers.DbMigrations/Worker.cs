using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Workers.DbMigrations;

public class Worker(
    IServiceProvider         serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime
) : BackgroundService
{
    public const            string         ActivitySourceName = "Migrations";
    private static readonly ActivitySource SActivitySource    = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        using var activity = SActivitySource.StartActivity("Migrating database", ActivityKind.Client);

        try
        {
            using var scope     = serviceProvider.CreateScope();
            var       dbContext = scope.ServiceProvider.GetRequiredService<GsbcDbContext>();

            await RunMigrationAsync(dbContext, cancellationToken);
            await SeedMedicalAsync(dbContext, cancellationToken);
            await SeedAllergensAsync(dbContext, cancellationToken);
            await SeedAttendanceItemTypesAsync(dbContext, cancellationToken);
            await SeedBibleAsync(dbContext, cancellationToken);
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
            // await dbContext.Database.MigrateAsync("20251209095101_1765273855", cancellationToken);
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
    }

    private static readonly string[] Medical =
    [
        "None",
        "ADHD",
        "Autism",
        "Asthma"
    ];

    private static async Task SeedMedicalAsync(GsbcDbContext dbContext, CancellationToken token)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Seed the database
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token);

            foreach (string medical in Medical)
            {
                if (await dbContext.MedicalTypes.AnyAsync(x => x.Label == medical, token))
                    continue;

                await dbContext.MedicalTypes.AddAsync(new DbMedicalType
                    {
                        Id = Guid.Empty,
                        Label = medical
                    },
                    token
                );
            }

            await dbContext.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        });
    }

    private static readonly string[] Allergens =
    [
        "None",
        "Dairy",
        "Gluten",
        "Soy",
        "Grass",
        "Nuts",
        "Eggs",
        "Honey",
        "Bees / Wasps",
        "Mosquitos / Mites / Sandflies"
    ];

    private static async Task SeedAllergensAsync(GsbcDbContext dbContext, CancellationToken token)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Seed the database
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token);

            foreach (string allergen in Allergens)
            {
                if (await dbContext.Allergens.AnyAsync(x => x.Label == allergen, token))
                    continue;

                await dbContext.Allergens.AddAsync(new DbAllergen
                    {
                        Id = Guid.Empty,
                        Label = allergen
                    },
                    token
                );
            }

            await dbContext.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        });
    }
    
    private static readonly DbAttendanceItemType[] AttendanceItemTypes =
    [
        new()
        {
            Id = Guid.Empty,
            Label = "Came Early",
            Reward = 1,
            RequiresReturning = false
        },
        new()
        {
            Id = Guid.Empty,
            Label = "Bible",
            Reward = 2,
            RequiresReturning = false
        },
        new()
        {
            Id = Guid.Empty,
            Label = "Phone",
            Reward = null,
            RequiresReturning = true
        }
    ];
    
    private static async Task SeedAttendanceItemTypesAsync(GsbcDbContext dbContext, CancellationToken token)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Seed the database
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token);

            foreach (DbAttendanceItemType itemType in AttendanceItemTypes)
            {
                if (await dbContext.AttendanceItemTypes.AnyAsync(x => x.Label == itemType.Label, token))
                    continue;

                await dbContext.AttendanceItemTypes.AddAsync(itemType, token);
            }

            await dbContext.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        });
    }

    private record CsvVerse(
        int    Book,
        int    Chapter,
        int    Versecount,
        string Verse
    );

    private record CsvBook(
        int    Id,
        string Book
    );

    private static async Task SeedBibleAsync(GsbcDbContext dbContext, CancellationToken cancellationToken)
    {
        using StreamReader booksReader = new("Data/bible-books.csv");
        using CsvReader    booksCsv    = new(booksReader, CultureInfo.InvariantCulture);

        using StreamReader versesReader = new("Data/bible-verses.csv");
        using CsvReader    versesCsv    = new(versesReader, CultureInfo.InvariantCulture);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Seed the database
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            if (await dbContext.BibleVerses.AnyAsync(cancellationToken))
                return;

            List<CsvBook> csvBooks = booksCsv.GetRecords<CsvBook>().ToList();

            List<DbBibleVerse> verses = [];
            await foreach (var csvVerse in versesCsv.GetRecordsAsync<CsvVerse>(cancellationToken))
            {
                DbBibleVerse verse = new()
                {
                    Id = Guid.Empty,

                    VerseNumber = csvVerse.Versecount,
                    Verse = csvVerse.Verse,

                    ChapterNumber = csvVerse.Chapter,
                    BookNumber = csvVerse.Book,
                    BookName = csvBooks.First(x => x.Id == csvVerse.Book).Book
                };
                verses.Add(verse);
            }

            await dbContext.BibleVerses.AddRangeAsync(verses, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}