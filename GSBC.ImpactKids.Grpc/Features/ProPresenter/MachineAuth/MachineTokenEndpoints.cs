using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.ProPresenter;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.MachineAuth;

/// <summary>
/// <c>POST machine/token</c> - where a ProPresenter agent turns an approval into a machine, and a
/// machine's secret into an access token.
/// <para>
/// <b>Anonymous and internet-reachable, unlike every other anonymous endpoint in this service.</b>
/// The <c>internal/</c> ones are safe because the internet cannot reach them; this one has to be
/// reached, because the agent asks it for a credential before it has one. What protects it instead:
/// both inputs it accepts are 32 random bytes, a code is single use, short lived and bound to a PKCE
/// verifier, and the endpoint is rate limited. Nothing about an attempt is logged either way - a
/// secret in a log is a secret in every log shipper downstream of it.
/// </para>
/// </summary>
public static class MachineTokenEndpoints
{
    public const string RateLimitPolicy = "machine-token";

    public static IEndpointRouteBuilder AddMachineTokenEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(ProPresenterEnrolment.TokenPath.TrimStart('/'), Token)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy);

        return builder;
    }

    private static Task<IResult> Token(
        ProPresenterTokenRequest                   body,
        GsbcDbContext                              db,
        MachineSigningKeyProvider                  signingKeys,
        IEventService<ProPresenterMachine>         eventService,
        CancellationToken                          token
    ) => body.GrantType switch
    {
        ProPresenterTokenRequest.AuthorizationCodeGrant => ExchangeCode(body, db, signingKeys, eventService, token),
        ProPresenterTokenRequest.ClientCredentialsGrant => IssueAccessToken(body, db, token),
        _                                               => Task.FromResult(Error(ProPresenterTokenResponse.InvalidRequest))
    };

    /// <summary>
    /// Spends an approval. Every check that fails also <b>burns the code</b>: a code that has been
    /// presented with the wrong verifier has been seen by somebody it was not meant for, and the
    /// leader can simply press Approve again.
    /// </summary>
    private static async Task<IResult> ExchangeCode(
        ProPresenterTokenRequest           body,
        GsbcDbContext                      db,
        MachineSigningKeyProvider          signingKeys,
        IEventService<ProPresenterMachine> eventService,
        CancellationToken                  token
    )
    {
        if (string.IsNullOrEmpty(body.Code) || string.IsNullOrEmpty(body.CodeVerifier)
            || string.IsNullOrEmpty(body.RedirectUri))
            return Error(ProPresenterTokenResponse.InvalidRequest);

        string codeHash = MachineSecrets.Hash(body.Code);

        DbProPresenterMachine? machine = await db.ProPresenterMachines
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EnrolmentCodeHash == codeHash, token);

        if (machine == null)
            return Error(ProPresenterTokenResponse.InvalidGrant);

        bool valid = machine.SecretHash == null
                     && machine.EnrolmentExpiresAt > DateTimeOffset.UtcNow
                     && machine.EnrolmentRedirectUri == body.RedirectUri
                     && machine.EnrolmentChallenge != null
                     && ProPresenterEnrolment.VerifierMatches(body.CodeVerifier, machine.EnrolmentChallenge);

        if (!valid)
        {
            await db.ProPresenterMachines
                .Where(x => x.Id == machine.Id && x.EnrolmentCodeHash == codeHash)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.EnrolmentCodeHash, (string?)null)
                    .SetProperty(x => x.EnrolmentChallenge, (string?)null)
                    .SetProperty(x => x.EnrolmentRedirectUri, (string?)null)
                    .SetProperty(x => x.EnrolmentExpiresAt, (DateTimeOffset?)null), token);

            return Error(ProPresenterTokenResponse.InvalidGrant);
        }

        string secret = MachineSecrets.Generate();

        // Claimed atomically: the WHERE re-checks the code is still pending, so two exchanges racing
        // with the same code cannot both come away with a secret. Exactly one row changes or none.
        int claimed = await db.ProPresenterMachines
            .Where(x => x.Id == machine.Id && x.EnrolmentCodeHash == codeHash && x.SecretHash == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.SecretHash, MachineSecrets.Hash(secret))
                .SetProperty(x => x.EnrolledAt, DateTimeOffset.UtcNow)
                .SetProperty(x => x.EnrolmentCodeHash, (string?)null)
                .SetProperty(x => x.EnrolmentChallenge, (string?)null)
                .SetProperty(x => x.EnrolmentRedirectUri, (string?)null)
                .SetProperty(x => x.EnrolmentExpiresAt, (DateTimeOffset?)null), token);

        if (claimed != 1)
            return Error(ProPresenterTokenResponse.InvalidGrant);

        // So the machine's first access token verifies in this process without waiting a tick.
        await signingKeys.RefreshAsync(token);
        await eventService.SendUpdatedEvent(token);

        // The secret's one appearance in the clear. Only its hash was written.
        return Results.Ok(new ProPresenterTokenResponse
        {
            MachineId = machine.Id,
            MachineSecret = secret
        });
    }

    private static async Task<IResult> IssueAccessToken(
        ProPresenterTokenRequest body,
        GsbcDbContext            db,
        CancellationToken        token
    )
    {
        if (body.MachineId == null || string.IsNullOrEmpty(body.MachineSecret))
            return Error(ProPresenterTokenResponse.InvalidRequest);

        var machine = await db.ProPresenterMachines
            .AsNoTracking()
            .Where(x => x.Id == body.MachineId && x.SecretHash != null)
            .Select(x => new { x.Id, x.SecretHash, x.TokenSigningKey })
            .FirstOrDefaultAsync(token);

        // One answer for "no such machine" and "wrong secret". The agent treats it as final and starts
        // setup again, which is right for a revoked machine and harmless for anything else.
        if (machine == null || !MachineSecrets.Matches(body.MachineSecret, machine.SecretHash!))
            return Results.Json(
                new ProPresenterTokenResponse { Error = ProPresenterTokenResponse.InvalidClient },
                statusCode: StatusCodes.Status401Unauthorized
            );

        return Results.Ok(new ProPresenterTokenResponse
        {
            AccessToken = MachineTokens.Mint(machine.Id, machine.TokenSigningKey),
            ExpiresIn = (int)MachineAuthDefaults.TokenLifetime.TotalSeconds
        });
    }

    private static IResult Error(string error) =>
        Results.BadRequest(new ProPresenterTokenResponse { Error = error });
}
