namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

/// <summary>
/// The RSS feeds the agent serves to ProPresenter on the same Mac. Shared so the admin page's setup
/// instructions and the agent cannot disagree about where a feed is.
/// <para>
/// Loopback only - ProPresenter reads them from the same machine, so nothing about a feed is ever
/// reachable from the network. See <c>docs/work/2026-10-propresenter-agent.md</c> for what
/// ProPresenter does with a feed: it shows only item titles ("Title" mode), runs every item's title
/// together with no separator, and re-fetches every 10 seconds without redrawing on its own.
/// </para>
/// </summary>
public static class ProPresenterFeeds
{
    public const int Port = 51080;

    public const string FirstTimersPath = "/feeds/first-timers.rss";

    public static string Url(string path) => $"http://127.0.0.1:{Port}{path}";
}
