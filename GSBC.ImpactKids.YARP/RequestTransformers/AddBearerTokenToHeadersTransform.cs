using System.Net.Http.Headers;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Yarp.ReverseProxy.Transforms;

namespace GSBC.ImpactKids.YARP.RequestTransformers;

internal sealed partial class AddBearerTokenToHeadersTransform(
    ILogger<AddBearerTokenToHeadersTransform> logger
) : RequestTransform
{
    public override async ValueTask ApplyAsync(RequestTransformContext context)
    {
        if (context.HttpContext.User.Identity is not { IsAuthenticated: true })
        {
            return;
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