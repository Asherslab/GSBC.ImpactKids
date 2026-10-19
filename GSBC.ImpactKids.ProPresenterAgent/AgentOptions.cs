using System.Reflection;

namespace GSBC.ImpactKids.ProPresenterAgent;

/// <summary>
/// The only settings that live on the Mac. Everything else - where ProPresenter is, what to show -
/// is pulled from Impact Kids, so the admin page is the one place anybody configures anything.
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>
    /// Impact Kids itself, from the <c>Agent__ImpactKidsUrl</c> environment variable: the LaunchAgent
    /// plist <c>install.sh</c> writes (production unless <c>--url</c> says otherwise), or the AppHost
    /// for a local run.
    /// <para>
    /// <b>Deliberately no default.</b> It used to default to production in <c>appsettings.json</c>,
    /// and a local run that missed its settings went straight at production and opened a browser on
    /// its connect page. Now a run that is not told where Impact Kids is refuses to start.
    /// </para>
    /// </summary>
    public Uri ImpactKidsUrl { get; set; } = null!;

    /// <summary>
    /// Where the credential is kept. Defaults to Application Support; overridden only so a local run
    /// cannot clobber the installed agent's credential on a developer's Mac.
    /// </summary>
    public string? DataDirectory { get; set; }

    public string ResolvedDataDirectory => DataDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "ImpactKids", "ProPresenterAgent"
    );

    /// <summary>The version the release job stamped, without the commit hash the SDK appends.</summary>
    public static string Version { get; } =
        (typeof(AgentOptions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
         ?? "unknown").Split('+')[0];
}
