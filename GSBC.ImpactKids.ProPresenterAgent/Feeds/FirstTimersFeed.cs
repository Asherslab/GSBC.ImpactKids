using GSBC.ImpactKids.ProPresenterAgent.ImpactKids;
using GSBC.ImpactKids.ProPresenterAgent.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.ProPresenterAgent.Feeds;

/// <summary>
/// The first names of children here for the first time today, comma separated, for a welcome on
/// screen. Asked of Impact Kids each time ProPresenter fetches - every 10 seconds - so a child signed
/// in at the desk appears within about ten seconds.
/// <para>
/// <b>If Impact Kids cannot be reached, the last answer stands.</b> A blip in the church internet
/// must not blank a welcome that is on the screen; the names were right a moment ago and will be
/// again.
/// </para>
/// </summary>
public sealed class FirstTimersFeed(
    ImpactKidsClient         impactKids,
    AgentState               state,
    ProPresenterApi          proPresenter,
    ILogger<FirstTimersFeed> logger
)
{
    private readonly LabelFeed _feed = new("First timers", proPresenter, logger);

    private string _lastKnown = "";

    public async Task<IResult> HandleAsync(CancellationToken token)
    {
        if (!state.Connected)
            return _feed.Serve("");

        try
        {
            await impactKids.EnsureAuthenticatedAsync(token);

            BasicReadMultipleResponse<FirstTimer> response =
                await impactKids.Agent.GetFirstTimersToday(new ProPresenterAgentRequest(), token);

            if (response.Success)
                _lastKnown = string.Join("\n", response.Entities.Select(x => $"{x.FirstName.Trim()} {x.LastName.Trim()}"));
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            // Revocation included: the main loop notices that on its own and sets the Mac up again.
            logger.LogDebug(exception, "Could not refresh the first timers; serving the last list");
        }

        return _feed.Serve(_lastKnown);
    }
}
