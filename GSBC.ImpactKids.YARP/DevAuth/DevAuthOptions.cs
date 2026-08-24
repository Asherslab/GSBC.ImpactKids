using Microsoft.Extensions.Options;

namespace GSBC.ImpactKids.YARP.DevAuth;

/// <summary>
/// A local sign in that skips Auth0, so the app can be driven on a laptop without a
/// Google SSO round trip. Off unless three separate things line up: the Development
/// environment, an explicit <c>DevAuth:Enabled</c> flag, and a signing key handed in at
/// launch. Nothing here has a usable default - a misconfigured deployment gets no bypass,
/// not a weak one.
/// </summary>
internal sealed class DevAuthOptions
{
    public const string SectionName = "DevAuth";

    /// <summary>Issuer stamped on locally minted tokens, so they are obvious in a log.</summary>
    public const string Issuer = "gsbc-dev-bypass";

    /// <summary>Claim on the cookie holding the local token. Never returned to the browser.</summary>
    public const string TokenClaimType = "dev_access_token";

    public bool Enabled { get; set; }

    /// <summary>
    /// Symmetric key shared with the gRPC service. Generated fresh by the AppHost on every
    /// run and passed through the environment, so it is never written down and every token
    /// dies with the process that issued it.
    /// </summary>
    public string? SigningKey { get; set; }

    /// <summary>
    /// Who to sign in as. Defaults to the sub the gRPC claims transformation already seeds
    /// as enabled - any other subject lands as a new, disabled user and every call comes
    /// back 403 until it is enabled on the users page.
    /// </summary>
    public string Subject { get; set; } = "google-oauth2|108820909534487863492";

    public string Name { get; set; } = "Dev Bypass";

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(12);

    /// <summary>A key too short to sign with is a configuration error, not a weaker mode.</summary>
    public bool HasUsableKey => (SigningKey?.Length ?? 0) >= 32;
}

internal static class DevAuthGate
{
    /// <summary>
    /// The single place that decides whether the bypass exists. Both the endpoint that
    /// mints tokens and the two paths that trust them ask this, so there is no way to
    /// enable one without the others.
    /// </summary>
    public static bool IsOpen(IHostEnvironment environment, IOptions<DevAuthOptions> options) =>
        environment.IsDevelopment() &&
        options.Value.Enabled &&
        options.Value.HasUsableKey;
}
