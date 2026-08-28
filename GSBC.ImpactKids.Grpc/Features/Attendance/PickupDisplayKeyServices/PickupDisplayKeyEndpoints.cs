using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Features.Authentication.DisplayAuth;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.PickupDisplayKeyServices;

/// <summary>
/// The two questions the proxy asks about the pickup display key, as plain HTTP rather than
/// gRPC - the proxy is a standalone app that references neither the contracts nor a gRPC
/// client, and this is two small reads.
/// <para>
/// <b>Unauthenticated, and reachable only from inside the cluster.</b> The proxy has no
/// route for <c>internal/</c>, so a request from outside falls through to the WASM catch-all
/// and gets <c>index.html</c>. The
/// proxy reaches these by talking to <c>http://grpc</c> directly. <b>Never add a proxy
/// route for <c>internal/</c></b> - <c>validate</c> is a key oracle, and the only thing
/// stopping it being brute forced from the internet is that the internet cannot reach it.
/// </para>
/// </summary>
public static class PickupDisplayKeyEndpoints
{
    public static IEndpointRouteBuilder AddPickupDisplayKeyEndpoints(this IEndpointRouteBuilder builder)
    {
        // Anonymous, and now explicitly so. The fallback authorization policy is
        // EnabledOnly, which would otherwise close these to the proxy - it calls them with
        // no token at all, because answering "is this key current" is the step that happens
        // BEFORE there is any credential to present.
        RouteGroupBuilder group = builder
            .MapGroup("internal/pickup-display-key")
            .AllowAnonymous();

        // Spends the key once, at enrolment. Answers with the generation the caller should
        // put on its cookie, and with the token the screen presents to this service from
        // then on - minting it here is what keeps the signing key inside the cluster: the
        // proxy carries a finished token and never sees what signed it.
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
            if (!PickupDisplayKeys.Matches(body.Key, key.KeyHash))
                return Results.Unauthorized();

            return Results.Ok(new KeyGenerationResponse(
                key.Id,
                DisplayTokens.Mint(key.Id, key.TokenSigningKey)
            ));
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

            // No token here - this answers "which key is current", which the proxy asks on
            // every request. A token belongs only to the enrolment that proved it holds the
            // key, and handing one out to an unauthenticated caller would make the whole
            // credential pointless.
            return Results.Ok(new KeyGenerationResponse(generation));
        });

        return builder;
    }

    /// <summary>Public because minimal API model binding has to see it, not because anything else should.</summary>
    public sealed record ValidateKeyRequest(string? Key);

    /// <summary>
    /// Null generation means no key has ever been minted. <paramref name="Token"/> is present
    /// only on a successful <c>validate</c> - see the remarks there.
    /// </summary>
    public sealed record KeyGenerationResponse(Guid? Generation, string? Token = null);
}
