namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.MachineAuth;

/// <summary>
/// Names shared by the token this service mints for a ProPresenter machine and the scheme that
/// validates it.
/// <para>
/// A machine is a <b>caller type</b>, like a display and unlike a leader: no row in Users, no
/// UserId, no Enabled claim. Unlike a display it is not read-only - it writes its own status - which
/// is why it is a third scheme rather than a widening of the display one.
/// </para>
/// </summary>
public static class MachineAuthDefaults
{
    /// <summary>The JWT bearer scheme a machine token authenticates on. Never the default scheme.</summary>
    public const string SchemeName = "ProPresenterMachine";

    /// <summary>
    /// The machine's id. Present on every machine token and on nothing else, so it doubles as
    /// "this caller is a machine" - <see cref="Policies.MachineOnly"/> tests it for exactly that.
    /// </summary>
    public const string MachineIdClaimType = "propresenter_machine";

    public const string Issuer   = "gsbc-propresenter-machine";
    public const string Audience = "gsbc-propresenter-machine";

    /// <summary>
    /// Short, unlike a display's year. A machine can always get another - it holds its secret and
    /// asks the token endpoint - so nothing is stranded by expiry, and a short life bounds what a
    /// token copied off the Mac is worth.
    /// </summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How long a leader's approval stays exchangeable. The agent exchanges within a second of the
    /// redirect landing; this only has to cover a slow browser.
    /// </summary>
    public static readonly TimeSpan EnrolmentCodeLifetime = TimeSpan.FromMinutes(2);
}
