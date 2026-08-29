using System.IO.Compression;
using System.Text;
using GSBC.ImpactKids.Grpc.Data;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.Photos;

/// <summary>
/// A zip of every photo, named for the person, for office staff to drag into Elvanto's own UI.
///
/// <para>
/// This exists because <b>nothing can push a photo back through the Elvanto API</b> — neither
/// <c>people/edit</c> nor <c>people/create</c> accepts a <c>picture</c> parameter, at the top level
/// or under <c>fields</c>. The app owns photos from the backfill onwards, and this is the agreed
/// substitute for a sync.
/// </para>
/// <para>
/// Streamed through the API like everything else. About 500 photos at ~35 KB is ~18 MB, which does
/// not justify a presigned URL or a staging object — and a presigned URL would need the object
/// store to have a public ingress, which is exactly what the design avoids.
/// </para>
/// </summary>
public static class PhotoExportEndpoints
{
    public static IEndpointRouteBuilder AddPhotoExportEndpoints(this IEndpointRouteBuilder builder)
    {
        // Leader only, by the service's EnabledOnly fallback - same as the photo endpoints.
        builder.MapGet("api/people/photos/export", async (
            HttpContext       http,
            GsbcDbContext     db,
            PhotoStore        store,
            ILoggerFactory    loggerFactory,
            CancellationToken token
        ) =>
        {
            ILogger logger = loggerFactory.CreateLogger(typeof(PhotoExportEndpoints));

            var people = await db.People
                .AsNoTracking()
                .Where(x => x.PhotoVersion != null && x.DeletedAtUtc == null)
                .OrderBy(x => x.FirstName)
                .ThenBy(x => x.LastName)
                .Select(x => new { x.Id, x.FirstName, x.LastName, x.PhotoVersion })
                .ToListAsync(token);

            http.Response.ContentType = "application/zip";
            http.Response.Headers.ContentDisposition =
                $"attachment; filename=\"impact-kids-photos-{DateTime.Now:yyyy-MM-dd}.zip\"";

            // ZipArchive writes the central directory synchronously when it is disposed, and Kestrel
            // disallows synchronous writes to the response body by default. Without this the
            // response is a 200 carrying a truncated archive with no central directory - "not a
            // zipfile" to every unzip tool - because the headers are long gone by the time the
            // dispose throws. There is no async ZipArchive to reach for instead, so sync IO is
            // enabled for this one request rather than for the server.
            IHttpBodyControlFeature? bodyControl = http.Features.Get<IHttpBodyControlFeature>();
            if (bodyControl is not null) bodyControl.AllowSynchronousIO = true;

            // Written straight to the response body rather than buffered into a MemoryStream first:
            // the whole point of streaming it is not to hold ~18 MB per concurrent caller.
            using ZipArchive zip = new(http.Response.Body, ZipArchiveMode.Create, leaveOpen: true);

            // Two people really are called the same thing, and a zip silently keeping only the last
            // of them is the kind of loss nobody notices until a child has no photo in Elvanto.
            Dictionary<string, int> used = new(StringComparer.OrdinalIgnoreCase);
            int written = 0;
            int missing = 0;

            foreach (var person in people)
            {
                PhotoStore.PhotoBytes? photo = await store.GetAsync(person.PhotoVersion!, token);

                if (photo is null)
                {
                    // Already logged loudly by the store. Skipping is right: a zip that is short one
                    // photo is useful, and one that fails halfway is not.
                    missing++;
                    continue;
                }

                string name = UniqueFileName(used, person.FirstName, person.LastName,
                    ExtensionFor(photo.ContentType));

                ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                await using Stream target = entry.Open();
                await target.WriteAsync(photo.Bytes, token);

                written++;
            }

            logger.LogInformation(
                "Photo export: {Written} photos zipped, {Missing} named by a row but absent from the store",
                written, missing);
        });

        return builder;
    }

    /// <summary>
    /// <c>firstname_lastname.jpg</c>, lower-cased, non-alphanumerics collapsed to <c>_</c>, with a
    /// numeric suffix on a collision (<c>asher_george_2.jpg</c>).
    /// </summary>
    private static string UniqueFileName(
        Dictionary<string, int> used,
        string                  firstName,
        string                  lastName,
        string                  extension)
    {
        string stem = $"{Slug(firstName)}_{Slug(lastName)}";
        if (stem == "_") stem = "person";

        if (used.TryGetValue(stem, out int count))
        {
            used[stem] = count + 1;
            return $"{stem}_{count + 1}{extension}";
        }

        used[stem] = 1;
        return $"{stem}{extension}";
    }

    private static string Slug(string value)
    {
        StringBuilder builder = new(value.Length);
        bool lastWasSeparator = false;

        foreach (char c in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().Trim('_');
    }

    /// <summary>
    /// Follows the stored content type rather than being hard-coded, so a PNG that got in stays a
    /// PNG and Elvanto is not handed a mislabelled file.
    /// </summary>
    private static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png"  => ".png",
        "image/webp" => ".webp",
        _            => ".jpg"
    };
}
