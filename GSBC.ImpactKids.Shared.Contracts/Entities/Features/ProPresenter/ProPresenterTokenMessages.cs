using System.Text.Json.Serialization;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

/// <summary>
/// The JSON bodies of <see cref="ProPresenterEnrolment.TokenPath"/>. Plain HTTP rather than gRPC:
/// the agent asks for a token before it has anything to authenticate a gRPC call with. Field names
/// follow RFC 6749 so the exchange reads like the standard it is.
/// </summary>
public sealed record ProPresenterTokenRequest
{
    public const string AuthorizationCodeGrant = "authorization_code";
    public const string ClientCredentialsGrant = "client_credentials";

    [JsonPropertyName("grant_type")]
    public string? GrantType { get; init; }

    // authorization_code - spent once, to become a machine
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("code_verifier")]
    public string? CodeVerifier { get; init; }

    [JsonPropertyName("redirect_uri")]
    public string? RedirectUri { get; init; }

    // client_credentials - every fifteen minutes, for an access token
    [JsonPropertyName("machine_id")]
    public Guid? MachineId { get; init; }

    [JsonPropertyName("machine_secret")]
    public string? MachineSecret { get; init; }
}

/// <summary>
/// One of two shapes, by grant: a new machine's id and secret (returned once, ever), or an access
/// token. <see cref="Error"/> follows RFC 6749 §5.2.
/// </summary>
public sealed record ProPresenterTokenResponse
{
    /// <summary>
    /// The credential is dead - the machine was revoked, or never existed. The agent wipes its
    /// Keychain entry and starts setup again. Anything else is worth retrying.
    /// </summary>
    public const string InvalidClient = "invalid_client";

    /// <summary>The code was wrong, spent, expired, or the verifier did not match it.</summary>
    public const string InvalidGrant = "invalid_grant";

    public const string InvalidRequest = "invalid_request";

    [JsonPropertyName("machine_id")]
    public Guid? MachineId { get; init; }

    [JsonPropertyName("machine_secret")]
    public string? MachineSecret { get; init; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
