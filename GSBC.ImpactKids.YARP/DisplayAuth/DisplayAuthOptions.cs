namespace GSBC.ImpactKids.YARP.DisplayAuth;

/// <summary>
/// The pickup wall's own authentication scheme. A screen on a wall cannot sign in, so it
/// enrols once from a keyed setup link and carries a cookie afterwards.
/// <para>
/// <b>This is a second scheme beside the leader session, never a widening of it.</b> It
/// grants exactly one thing - calling
/// <c>public/GSBC.ImpactKids.Attendance.Display</c> - and two independent things stop it
/// granting more: the <c>Default</c> policy on every <c>gRPC/</c> and <c>/api/</c> route
/// names only <see cref="Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme"/>,
/// and the proxy attaches no bearer token on the pickup route, so nothing this cookie
/// carries can ever satisfy the gRPC service's <c>EnabledOnly</c> policy.
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
    /// Where a screen lands after enrolling, and the only place it is allowed to land -
    /// the return url is checked against this prefix so the enrolment link can never be
    /// turned into an open redirect.
    /// </summary>
    public const string DisplayPath = "/Display/Pickup";

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
