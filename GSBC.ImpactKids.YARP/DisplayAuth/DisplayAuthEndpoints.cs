using System.Security.Claims;
using GSBC.ImpactKids.YARP.Extensions;
using Microsoft.AspNetCore.Authentication;

namespace GSBC.ImpactKids.YARP.DisplayAuth;

/// <summary>
/// Enrolment for the pickup wall. The key rides the query string <b>once</b>, is spent
/// here, and a cookie carries the screen afterwards - the same shape as
/// <see cref="DevAuth.DevAuthEndpoints"/>: mint a session, redirect to a clean URL.
/// <para>
/// The one-shot part is the point.
/// A wall display holds its session for months and re-reads on every change all night; a key
/// left in the query string would be written into proxy and CDN access logs on every one of
/// those requests, forever. Spent once at enrolment, it appears there once.
/// </para>
/// <para>
/// Unlike the dev bypass these routes are not gated by environment - a pickup wall is a
/// production thing, and there is no other way to set one up.
/// </para>
/// </summary>
internal static class DisplayAuthEndpoints
{
    internal static IEndpointRouteBuilder MapDisplayAuthEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("display-login", async (
            string?                key,
            string?                returnUrl,
            HttpContext            context,
            PickupDisplayKeyClient keys
        ) =>
        {
            if (string.IsNullOrWhiteSpace(key))
                return Results.Text(
                    "This link is missing its setup key. Open the pickup display link from the admin page.",
                    statusCode: StatusCodes.Status400BadRequest
                );

            PickupDisplayKeyClient.DisplayEnrolment? enrolment =
                await keys.ValidateAsync(key, context.RequestAborted);

            // Readable words rather than a bare 401 - a person is standing at the TV when
            // this happens, and "rotate and use the new link" is the whole remedy.
            if (enrolment == null)
                return Results.Text(
                    "That setup key is not valid. It may have been rotated - open the pickup display link from the admin page again.",
                    statusCode: StatusCodes.Status401Unauthorized
                );

            ClaimsIdentity identity = new(
                [
                    // The screen has no identity beyond "enrolled on this key". Nothing here
                    // names a person, and nothing here is a UserId the gRPC side would look up.
                    new Claim(ClaimTypes.NameIdentifier, "pickup-display"),
                    new Claim(DisplayAuthOptions.GenerationClaimType, enrolment.Generation.ToString()),
                    // The gRPC service's own credential for this screen, minted just now and
                    // carried from here on. See DisplayAuthOptions.TokenClaimType.
                    new Claim(DisplayAuthOptions.TokenClaimType, enrolment.Token)
                ],
                DisplayAuthOptions.SchemeName,
                nameType: ClaimTypes.NameIdentifier,
                roleType: ClaimTypes.Role
            );

            await context.SignInAsync(
                DisplayAuthOptions.SchemeName,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = DateTimeOffset.UtcNow.Add(DisplayAuthOptions.CookieLifetime)
                }
            );

            // The key does not travel on. Redirecting to the keyed URL, or appending it to
            // the destination, would put it straight back into the access log this whole
            // dance exists to keep it out of.
            return Results.Redirect(context.BuildRedirectUrl(DisplayPath(returnUrl)));
        });

        builder.MapGet("display-logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(DisplayAuthOptions.SchemeName);

            return Results.Redirect(context.BuildRedirectUrl("/"));
        });

        return builder;
    }

    /// <summary>
    /// Where the screen lands. A return url is honoured only when it is one of the wall
    /// displays - <c>/Display/Pickup</c>, <c>/Display/Scores</c>, <c>/Display/Reveal</c>, with
    /// or without a service id - so the enrolment link cannot be dressed up as a redirect to
    /// somewhere else.
    /// </summary>
    private static string DisplayPath(string? returnUrl) =>
        returnUrl != null
        && returnUrl.StartsWith(DisplayAuthOptions.DisplayPathPrefix, StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : DisplayAuthOptions.DefaultDisplayPath;
}
