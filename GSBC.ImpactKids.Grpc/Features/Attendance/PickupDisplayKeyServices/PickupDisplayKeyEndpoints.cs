using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.PickupDisplayKeyServices;

/// <summary>
/// The two questions the proxy asks about the pickup display key, as plain HTTP rather than
/// gRPC - the proxy is a standalone app that references neither the contracts nor a gRPC
/// client, and this is two small reads.
/// <para>
/// <b>Unauthenticated, and reachable only from inside the cluster.</b> The proxy matches
/// <c>public/</c> one named service at a time and has no route for <c>internal/</c>, so a
/// request from outside falls through to the WASM catch-all and gets <c>index.html</c>. The
/// proxy reaches these by talking to <c>http://grpc</c> directly. <b>Never add a proxy
/// route for <c>internal/</c></b> - <c>validate</c> is a key oracle, and the only thing
/// stopping it being brute forced from the internet is that the internet cannot reach it.
/// </para>
/// </summary>
public static class PickupDisplayKeyEndpoints
{
    public static IEndpointRouteBuilder AddPickupDisplayKeyEndpoints(this IEndpointRouteBuilder builder)
    {
        RouteGroupBuilder group = builder.MapGroup("internal/pickup-display-key");

        // Spends the key once, at enrolment. Answers with the generation the caller should
        // put on its cookie, so a later rotation can tell that cookie is stale.
        group.MapPost("validate", async (
            ValidateKeyRequest body,
            GsbcDbContext      db,
            CancellationToken  token
        ) =>
        {
            if (string.IsNullOrEmpty(body.Key))
                return Results.Unauthorized();

            DbPickupDisplayKey? key = await db.PickupDisplayKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(token);

            // Nothing minted yet, so nothing matches. Not an error - the wall simply has no
            // key to enrol against until an admin makes one.
            if (key == null)
                return Results.Unauthorized();

            // Constant time, and nothing about the attempt is logged either way. A failure
            // log that echoes the key is the same leak as a success log that does.
            return PickupDisplayKeys.Matches(body.Key, key.KeyHash)
                ? Results.Ok(new KeyGenerationResponse(key.Id))
                : Results.Unauthorized();
        });

        // Which key is current. Carries no secret - it is a generation marker, and the proxy
        // polls it to notice a rotation and drop screens enrolled on the old key.
        group.MapGet("generation", async (
            GsbcDbContext     db,
            CancellationToken token
        ) =>
        {
            Guid? generation = await db.PickupDisplayKeys
                .AsNoTracking()
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(token);

            return Results.Ok(new KeyGenerationResponse(generation));
        });

        return builder;
    }

    /// <summary>Public because minimal API model binding has to see it, not because anything else should.</summary>
    public sealed record ValidateKeyRequest(string? Key);

    /// <summary>Null generation means no key has ever been minted.</summary>
    public sealed record KeyGenerationResponse(Guid? Generation);
}
