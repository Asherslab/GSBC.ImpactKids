namespace GSBC.ImpactKids.Grpc.Features.Authentication.DisplayAuth;

/// <summary>
/// The names and claim types shared by the two halves of the display credential - the token
/// this service mints at enrolment, and the scheme this service validates it with.
/// <para>
/// A display is a <b>caller type</b>, not a user. It has no row in Users, no UserId and no
/// Enabled claim, and it never gets one: see <see cref="Policies"/> for how that is enforced
/// rather than merely true.
/// </para>
/// </summary>
public static class DisplayAuthDefaults
{
    /// <summary>The JWT bearer scheme a display token authenticates on. Never the default scheme.</summary>
    public const string SchemeName = "Display";

    /// <summary>
    /// Which enrolment key generation the screen holds. Present on every display token and on
    /// nothing else, so it doubles as "this caller is a display" - <see cref="Policies"/>
    /// tests it for exactly that.
    /// </summary>
    public const string GenerationClaimType = "display_generation";

    public const string Issuer   = "gsbc-display";
    public const string Audience = "gsbc-display";

    /// <summary>
    /// How long a minted token is good for. Long on purpose - a token that expires on its own
    /// strands a TV mid service, and the screen has nobody standing at it to re-enrol.
    /// <para>
    /// Expiry is not what ends a display's access; <b>rotation</b> is. Each rotation mints a
    /// new signing key, so every token issued under the old one stops verifying the moment the
    /// new key is picked up - see <see cref="DisplaySigningKeyProvider"/>.
    /// </para>
    /// </summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(365);
}
