using System.Diagnostics;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Photos;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Workers.PhotoBackfill;

/// <summary>
/// A one-off pull of the photos Elvanto already holds, so leaders start with a head start rather
/// than a blank roll.
///
/// <para>
/// <b>Deliberately not part of the sync service.</b> It runs once, it makes hundreds of outbound
/// HTTP calls for bytes, and the sync engine's plan/execute contract has nothing to say about
/// bytes. It is also read-only against Elvanto — there is no way to write a picture through their
/// API at all, which is why the app owns photos from here on and why <c>B5</c>'s export exists.
/// </para>
/// <para>
/// <b>Expect roughly 60% of attempted fetches to succeed, and that is the healthy result.</b> Over
/// half of the real photo URLs in this account are malformed by Elvanto itself — a botched
/// migration concatenated a second URL into the thumb suffix — and those 403 permanently, with
/// <c>people/getInfo</c> returning the identical broken URL. There is no repair path, so they are
/// filtered out where possible and treated as final when they are not.
/// </para>
/// </summary>
public class Worker(
    IServiceProvider         serviceProvider,
    IHttpClientFactory       httpClientFactory,
    ILogger<Worker>          logger,
    IHostApplicationLifetime hostApplicationLifetime
) : BackgroundService
{
    public const            string         ActivitySourceName = "PhotoBackfill";
    private static readonly ActivitySource SActivitySource    = new(ActivitySourceName);

    /// <summary>
    /// The only URL shape that is a real upload. The other two Elvanto returns — its own
    /// <c>default-avatar.svg</c> and a gravatar fallback — are placeholders, and storing one would
    /// be worse than storing nothing: the person would read as "has a photo" and be hidden from the
    /// Photos tool forever.
    /// </summary>
    private const string RealPhotoHost = "d2dek0x2lg6bxh.cloudfront.net";

    /// <summary>
    /// The malformed marker. A real thumb suffix is a unix timestamp; these have a whole second URL
    /// concatenated in. Cheaper to skip than to fetch and be 403'd, and it keeps the failure count
    /// meaningful.
    /// </summary>
    private const string MalformedMarker = "_thumb_http";

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        using Activity? activity = SActivitySource.StartActivity("Backfilling photos", ActivityKind.Client);

        try
        {
            await RunAsync(token);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            logger.LogError(ex, "Photo backfill failed");
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private async Task RunAsync(CancellationToken token)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        GsbcDbContext  db      = scope.ServiceProvider.GetRequiredService<GsbcDbContext>();
        ElvantoService elvanto = scope.ServiceProvider.GetRequiredService<ElvantoService>();
        PhotoStore     store   = scope.ServiceProvider.GetRequiredService<PhotoStore>();
        PhotoStoreConfig config = scope.ServiceProvider.GetRequiredService<PhotoStoreConfig>();

        await store.EnsureBucketAsync(token);

        // Only children who have actually turned up. Most of the 1754-person roll is never seen at
        // Impact Kids, and pulling their photos is wasted storage and wasted requests.
        HashSet<Guid> attended = (await db.AttendanceRecords
                .AsNoTracking()
                .Where(x => !x.Deleted)
                .Select(x => x.PersonId)
                .Distinct()
                .ToListAsync(token))
            .ToHashSet();

        List<DbPerson> candidates = await db.People
            .Where(x => x.PhotoVersion == null && x.ElvantoId != null)
            .ToListAsync(token);

        candidates = candidates.Where(x => attended.Contains(x.Id)).ToList();

        logger.LogInformation(
            "Photo backfill: {Attended} people have attended, {Candidates} of them have an Elvanto id and no photo",
            attended.Count, candidates.Count);

        Dictionary<string, ElvantoPerson> byElvantoId = (await elvanto.GetPeopleWithPicturesAsync(token))
            .Where(x => x.Id != null)
            .ToDictionary(x => x.Id!, x => x);

        Counters counters = new() { Considered = candidates.Count };

        using HttpClient http = httpClientFactory.CreateClient("elvanto-photos");

        foreach (DbPerson person in candidates)
        {
            if (token.IsCancellationRequested) break;

            if (config.IsBlockedByMediaConsent(person.MediaConsent))
            {
                counters.SkippedConsent++;
                continue;
            }

            if (!byElvantoId.TryGetValue(person.ElvantoId!, out ElvantoPerson? elv))
            {
                counters.SkippedNoElvantoRow++;
                continue;
            }

            string? picture = elv.Picture;

            if (string.IsNullOrWhiteSpace(picture) || !picture.Contains(RealPhotoHost, StringComparison.OrdinalIgnoreCase))
            {
                counters.SkippedPlaceholder++;
                continue;
            }

            if (picture.Contains(MalformedMarker, StringComparison.OrdinalIgnoreCase))
            {
                counters.SkippedMalformed++;
                continue;
            }

            byte[]? bytes = await FetchAsync(http, picture, person, counters, token);
            if (bytes is null) continue;

            string version = await store.PutAsync(bytes, "image/jpeg", token);

            person.PhotoVersion = version;
            counters.Fetched++;
        }

        await db.SaveChangesAsync(token);

        logger.LogInformation(
            "Photo backfill finished. considered={Considered} fetched={Fetched} "
            + "skipped-placeholder={SkippedPlaceholder} skipped-malformed={SkippedMalformed} "
            + "skipped-consent={SkippedConsent} skipped-no-elvanto-row={SkippedNoElvantoRow} "
            + "failed={Failed}",
            counters.Considered, counters.Fetched, counters.SkippedPlaceholder,
            counters.SkippedMalformed, counters.SkippedConsent, counters.SkippedNoElvantoRow,
            counters.Failed);
    }

    /// <summary>
    /// One attempt, and only one. A 403 here is permanent — it is Elvanto's own broken URL, and
    /// <c>getInfo</c> hands back the identical one — so a retry is a wasted request and a slower
    /// run, not a second chance.
    /// </summary>
    private async Task<byte[]?> FetchAsync(
        HttpClient        http,
        string            url,
        DbPerson          person,
        Counters          counters,
        CancellationToken token)
    {
        try
        {
            using HttpResponseMessage response = await http.GetAsync(url, token);

            if (!response.IsSuccessStatusCode)
            {
                counters.Failed++;
                logger.LogWarning(
                    "Photo for {FirstName} {LastName} ({PersonId}) answered {Status}; not retrying",
                    person.FirstName, person.LastName, person.Id, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            counters.Failed++;
            logger.LogWarning(ex, "Photo for {PersonId} could not be fetched", person.Id);
            return null;
        }
    }

    private sealed class Counters
    {
        public int Considered;
        public int Fetched;
        public int SkippedPlaceholder;
        public int SkippedMalformed;
        public int SkippedConsent;
        public int SkippedNoElvantoRow;
        public int Failed;
    }
}
