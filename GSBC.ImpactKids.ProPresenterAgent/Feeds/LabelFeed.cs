using System.Net;
using GSBC.ImpactKids.ProPresenterAgent.ProPresenter;

namespace GSBC.ImpactKids.ProPresenterAgent.Feeds;

/// <summary>
/// One line of text for a ProPresenter text box, served as RSS - the shape ProPresenter's "linked to
/// RSS feed" text actually works with, found by trying it on 2026-10-19:
/// <list type="bullet">
/// <item><b>Exactly one item.</b> In Title mode ProPresenter shows every item's title run together
/// with no separator, so the text is the one item's title and nothing else.</item>
/// <item><b>The redraw is ours to cause.</b> ProPresenter re-fetches every 10 seconds but does not
/// repaint. So when a fetch hands it different text from the last fetch, the stage is told to redraw
/// half a second later - after ProPresenter has parsed what it just got, not on a guess.</item>
/// </list>
/// </summary>
public sealed class LabelFeed(string name, ProPresenterApi proPresenter, ILogger logger)
{
    /// <summary>How long after a changed fetch the stage is redrawn - and the schedule's timer started, to land with it.</summary>
    public static readonly TimeSpan RedrawDelay = TimeSpan.FromMilliseconds(500);

    private string? _lastServed;

    public IResult Serve(string text)
    {
        if (!string.Equals(_lastServed, text, StringComparison.Ordinal))
        {
            logger.LogInformation("{Feed} now reads \"{Text}\"", name, text);
            _lastServed = text;

            _ = Task.Run(async () =>
            {
                await Task.Delay(RedrawDelay);
                await proPresenter.RedrawStageAsync(CancellationToken.None);
            });
        }

        string escaped = WebUtility.HtmlEncode(text);

        string rss = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel>
            <title>{WebUtility.HtmlEncode(name)}</title><link>http://127.0.0.1/</link><description>Impact Kids</description>
            <item><title>{escaped}</title></item>
            </channel></rss>
            """;

        return Results.Text(rss, "application/rss+xml; charset=utf-8");
    }
}
