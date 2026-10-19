using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

/// <summary>
/// The rules of connecting a ProPresenter machine (OAuth 2.0 for native apps, RFC 8252, with PKCE,
/// RFC 7636), in one place for all three parties: the agent builds a challenge with them, the
/// connect page checks a link with them before a leader approves, and the server enforces them.
/// </summary>
public static class ProPresenterEnrolment
{
    /// <summary>The page the agent opens in the browser. Query: <c>redirect_uri</c>, <c>state</c>, <c>code_challenge</c>, <c>code_challenge_method</c>, <c>name</c>.</summary>
    public const string ConnectPath = "/ProPresenter/Connect";

    /// <summary>The token endpoint, on the gRPC service and routed straight through the proxy.</summary>
    public const string TokenPath = "/machine/token";

    public const string ChallengeMethod = "S256";

    /// <summary>
    /// <b>The rule that keeps the connect page from being a phishing kit.</b> PKCE stops a code being
    /// stolen in flight; it does nothing against somebody who starts the flow themselves, keeps their
    /// own verifier, and sends a leader the link. Only ever redirecting to the loopback address means
    /// the code can only land on the computer whose browser approved it.
    /// <para>
    /// <c>http</c> only, any port, and the literal addresses only - not <c>localhost</c>, which
    /// RFC 8252 §8.3 warns can resolve somewhere else.
    /// </para>
    /// </summary>
    public static bool IsLoopbackRedirect(string? redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out Uri? uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp)
            return false;

        // UserInfo would let "http://127.0.0.1@evil.example/" read as loopback to a careless check.
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            return false;

        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out IPAddress? address)
               && (address.Equals(IPAddress.Loopback) || address.Equals(IPAddress.IPv6Loopback));
    }

    /// <summary>A challenge is the base64url SHA-256 of the verifier: 43 characters, no padding.</summary>
    public static bool IsWellFormedChallenge(string? challenge, string? method) =>
        method == ChallengeMethod
        && challenge is { Length: 43 }
        && challenge.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static string ComputeChallenge(string verifier) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>Constant time, so a wrong verifier says nothing about how wrong it was.</summary>
    public static bool VerifierMatches(string verifier, string challenge) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(ComputeChallenge(verifier)),
            Encoding.ASCII.GetBytes(challenge)
        );
}
