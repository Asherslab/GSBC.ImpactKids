using System.Collections.Concurrent;
using GSBC.ImpactKids.ProPresenterAgent.ProPresenter;
using GSBC.ImpactKids.ProPresenterAgent.Stage;

namespace GSBC.ImpactKids.ProPresenterAgent.Feeds;

/// <summary>
/// The schedule's labels for ProPresenter: what is on now (per group with <c>?group=</c>) and what is
/// next. One <see cref="LabelFeed"/> per distinct feed, so each forces the stage redraw when its own
/// text changes.
/// </summary>
public sealed class ScheduleFeeds(StageDriver stage, ProPresenterApi proPresenter, ILogger<ScheduleFeeds> logger)
{
    private readonly ConcurrentDictionary<string, LabelFeed> _feeds = new(StringComparer.OrdinalIgnoreCase);

    private LabelFeed Feed(string name) => _feeds.GetOrAdd(name, n => new LabelFeed(n, proPresenter, logger));

    public IResult Now(string? group)
    {
        if (!string.IsNullOrWhiteSpace(group))
            return Feed($"Now ({group})").Serve(stage.NowLabel(group));

        // The stage's own label: the schedule's timer waits for this fetch, to start with the redraw.
        string  label  = stage.NowLabel(null);
        IResult result = Feed("Now").Serve(label);
        stage.NowFeedServed(label);
        return result;
    }

    public IResult Next() => Feed("Next").Serve(stage.NextLabel());
}
