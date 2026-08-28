namespace GSBC.ImpactKids.YARP.DisplayAuth;

/// <summary>
/// The pickup wall's own authentication scheme. A screen on a wall cannot sign in, so it
/// enrols once from a keyed setup link and carries a cookie afterwards.
/// <para>
/// <b>This is a second scheme beside the leader session, never a widening of it.</b> A
/// display now reaches the ordinary <c>gRPC/</c> routes rather than a service of its own, so
/// what it may actually do is decided at the gRPC service by the policy on each method - only
/// those marked <c>EnabledOrDisplay</c> admit it, and a display can never write at all. The
/// proxy's job is narrower than it was: prove the screen is enrolled on the current key, and
/// attach the token that says so.
/// <para>
/// Nothing this cookie carries can satisfy the gRPC service's <c>EnabledOnly</c> policy,
/// because that policy names only the leader's bearer scheme - a display token is not merely
/// rejected by it, it is never authenticated against it.
/// </para>
/// </para>
/// </summary>
internal static class DisplayAuthOptions
{
    public const string SchemeName = "PickupDisplay";

    /// <summary>Named on the pickup route in <c>appsettings.json</c>. Nothing else may use it.</summary>
    public const string PolicyName = "PickupDisplay";

    public const string CookieName = "__gsbc_display";

    /// <summary>
    /// Which key the screen enrolled on. Compared against the current generation on every
    /// request, which is what makes a rotation total rather than merely forward-looking.
    /// </summary>
    public const string GenerationClaimType = "pickup_display_generation";

    /// <summary>
    /// The bearer token the gRPC service minted for this screen at enrolment, carried on the
    /// cookie and attached to every proxied request. The same shape as the dev bypass's token
    /// claim, and for the same reason: there is no token endpoint to fetch it from later, so
    /// it rides on the session it was issued with.
    /// <para>
    /// The proxy cannot mint or verify this - it holds no signing key. It is a sealed
    /// envelope carried from the enrolment call to the gRPC service.
    /// </para>
    /// </summary>
    public const string TokenClaimType = "pickup_display_token";

    /// <summary>
    /// The only place an enrolling screen is allowed to land. The return url is checked
    /// against this prefix so the enrolment link can never be turned into an open redirect.
    /// <para>
    /// A prefix rather than one page, because there is more than one wall now: the pickup
    /// list, the scoreboard and the reveal all enrol on this key and all live under
    /// <c>/Display</c>. Widening it further would hand the enrolment link the whole app to
    /// redirect to.
    /// </para>
    /// </summary>
    public const string DisplayPathPrefix = "/Display";

    /// <summary>Where a screen lands when the link names no page. The pickup wall is the common case.</summary>
    public const string DefaultDisplayPath = "/Display/Pickup";

    /// <summary>
    /// Deliberately long. A key that expires on its own strands a TV mid service, which is
    /// worse than the risk it manages - the key is non-expiring by decision, and rotation
    /// is how it ends. See <c>docs/modules/auth/sign-in.md</c>.
    /// </summary>
    public static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

    /// <summary>
    /// How long the proxy trusts its cached answer to "which key is current". The upper
    /// bound on how long an already-enrolled screen keeps working after a rotation - short
    /// enough to be "immediate" for a person watching a wall, long enough that a stream
    /// reconnect storm does not become a query storm.
    /// </summary>
    public static readonly TimeSpan GenerationCacheLifetime = TimeSpan.FromSeconds(30);
}
