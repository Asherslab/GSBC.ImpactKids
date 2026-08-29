using GSBC.ImpactKids.Grpc.Data;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.Photos;

/// <summary>
/// Serving a person's photo, as plain HTTP rather than gRPC.
///
/// <c>/api/{**catch-all}</c> already routes here through YARP, and the cookie the browser holds is
/// attached automatically to an <c>&lt;img&gt;</c> request on the same origin — so there is no JS,
/// no token plumbing and no fetch wrapper. The face is an ordinary image to the browser, which is
/// the whole point: it rides ordinary browser caching.
///
/// <b>Leader only, and structurally so.</b> This group carries no <c>AllowDisplay</c> marking and no
/// <c>EnabledOrDisplay</c> policy, so it falls through to the service's <c>EnabledOnly</c> fallback
/// and an enrolled wall display cannot reach it. That is a decision, not an omission: a
/// wall-mounted TV showing children's faces to a foyer is a different thing from a leader's phone.
/// See <see cref="Policies"/> for why the fallback is what makes it structural.
/// </summary>
public static class PersonPhotoEndpoints
{
    public static IEndpointRouteBuilder AddPersonPhotoEndpoints(this IEndpointRouteBuilder builder)
    {
        RouteGroupBuilder group = builder.MapGroup("api/people/{id:guid}/photo");

        group.MapGet("", async (
            Guid              id,
            string?           v,
            HttpContext       http,
            GsbcDbContext     db,
            PhotoStore        store,
            CancellationToken token
        ) =>
        {
            string? version = await db.People
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => x.PhotoVersion)
                .FirstOrDefaultAsync(token);

            // No person, or a person with no photo. Both are 404, and the client must treat that as
            // normal - PersonAvatar renders the coloured initial and never shows a broken image.
            if (version is null)
                return Results.NotFound();

            // The caller asked for a version that is no longer current. Answering with the current
            // one under the old URL would poison that URL in the browser cache for a year, since the
            // response is marked immutable. Refusing sends the client back for a fresh Person.
            if (v is not null && v != version)
                return Results.NotFound();

            PhotoStore.PhotoBytes? photo = await store.GetAsync(version, token);
            if (photo is null)
                return Results.NotFound();

            // A year, immutable, private. Safe only because the version is in the URL: a re-shot
            // photo is a different URL, so there is no revalidation, no ETag round trip and no
            // stale face. Private because this is a child's face behind a leader's session, and a
            // shared cache must not hold it.
            //
            // Set here rather than on the whole group on purpose. Every 404 above is a state that
            // can change - a child who has no photo yet will have one tonight - and caching one of
            // those immutably for a year would hide the new photo behind the old absence.
            http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";

            return Results.File(photo.Bytes, photo.ContentType);
        });

        return builder;
    }
}
