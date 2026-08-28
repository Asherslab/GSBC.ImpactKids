using System.Net.Http.Headers;
using Duende.AccessTokenManagement;
using Microsoft.AspNetCore.Authentication.Cookies;
using Duende.AccessTokenManagement.OpenIdConnect;
using GSBC.ImpactKids.YARP.DevAuth;
using GSBC.ImpactKids.YARP.DisplayAuth;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Transforms;

namespace GSBC.ImpactKids.YARP.RequestTransformers;

internal sealed partial class AddBearerTokenToHeadersTransform(
    ILogger<AddBearerTokenToHeadersTransform> logger,
    IHostEnvironment                          environment,
    IOptions<DevAuthOptions>                  devAuthOptions
) : RequestTransform
{
    public override async ValueTask ApplyAsync(RequestTransformContext context)
    {
        if (context.HttpContext.User.Identity is not { IsAuthenticated: true })
        {
            return;
        }

        // A wall display. Its token was minted by the gRPC service at enrolment and has been
        // riding on the display cookie ever since, so there is nothing to fetch or refresh -
        // and nothing for the token manager below to do, which is why this returns rather
        // than falling through. Asking Duende for a token on a session Auth0 never issued
        // fails on every request and logs an error saying so.
        //
        // A LEADER SESSION WINS when both cookies are present, and that case is the normal
        // one rather than an edge: the person who sets a TV up enrols it from the browser
        // they also work in, so their laptop holds both. The authorization policy authenticates
        // both schemes and merges the identities, so without this check the display token -
        // which is only ever allowed to read - would be attached to that person's requests and
        // silently demote them on every write they attempted.
        bool isLeader = context.HttpContext.User.Identities.Any(identity =>
            identity.IsAuthenticated
            && identity.AuthenticationType == CookieAuthenticationDefaults.AuthenticationScheme
        );

        string? displayToken = context.HttpContext.User
            .FindFirst(DisplayAuthOptions.TokenClaimType)?.Value;

        if (!isLeader && displayToken != null)
        {
            context.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", displayToken);
            return;
        }

        // A locally minted token, from a session the dev bypass handed out. Auth0 never saw
        // this user, so there is nothing for the token manager to fetch or refresh - the
        // token rides on the cookie. Unreachable unless the bypass is open.
        if (DevAuthGate.IsOpen(environment, devAuthOptions))
        {
            string? devToken = context.HttpContext.User.FindFirst(DevAuthOptions.TokenClaimType)?.Value;

            if (devToken != null)
            {
                context.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", devToken);
                return;
            }
        }

        // This also handles token refreshes
        TokenResult<UserToken> accessToken = await context.HttpContext.GetUserAccessTokenAsync();
        LogTokenTimeLeftTokenTimeLeft(logger, (accessToken.Token?.Expiration - DateTime.Now)?.ToString("g"));
        if (!accessToken.Succeeded)
        {
            LogCouldNotGetAccessTokenGetUserAccessTokenErrorForRequestPathRequestPathError(logger,
                accessToken.FailedResult.Error, context.HttpContext.Request.Path.Value,
                accessToken.FailedResult.ErrorDescription);
            return;
        }

        LogAddingBearerTokenToRequestHeadersForRequestPathRequestPath(logger, context.HttpContext.Request.Path.Value);
        context.ProxyRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken.Token.AccessToken);
    }

    [LoggerMessage(LogLevel.Error,
        "Could not get access token: {GetUserAccessTokenError} for request path: {RequestPath}. {Error}")]
    static partial void LogCouldNotGetAccessTokenGetUserAccessTokenErrorForRequestPathRequestPathError(
        ILogger<AddBearerTokenToHeadersTransform> logger,
        string?                                   getUserAccessTokenError,
        string?                                   requestPath,
        string?                                   error
    );

    [LoggerMessage(LogLevel.Information, "Adding bearer token to request headers for request path: {RequestPath}")]
    static partial void LogAddingBearerTokenToRequestHeadersForRequestPathRequestPath(
        ILogger<AddBearerTokenToHeadersTransform> logger,
        string?                                   requestPath
    );

    [LoggerMessage(LogLevel.Information, "Token Time Left: {TokenTimeLeft}")]
    static partial void LogTokenTimeLeftTokenTimeLeft(
        ILogger<AddBearerTokenToHeadersTransform> logger,
        string?                                   tokenTimeLeft
    );
}