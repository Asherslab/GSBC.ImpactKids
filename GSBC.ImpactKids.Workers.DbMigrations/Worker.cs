using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
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
            await dbContext.Database.MigrateAsync(cancellationToken);
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

            dbContext.BibleVerses.RemoveRange(await dbContext.BibleVerses.ToListAsync(cancellationToken));
            await dbContext.SaveChangesAsync(cancellationToken);

            List<CsvBook> csvBooks = booksCsv.GetRecords<CsvBook>().ToList();
            
            List<DbBibleVerse>   verses   = [];
            await foreach (var csvVerse in versesCsv.GetRecordsAsync<CsvVerse>(cancellationToken))
            {
                DbBibleVerse verse = new()
                {
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