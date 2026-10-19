using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

namespace GSBC.ImpactKids.ProPresenterAgent;

/// <summary>What the agent currently knows, shared between the main loop that refreshes it and the feeds that read it.</summary>
public sealed class AgentState
{
    /// <summary>Null until the first successful pull from Impact Kids, and while the Mac is not connected.</summary>
    public volatile ProPresenterAgentConfiguration? Configuration;

    /// <summary>False while the Mac is waiting to be approved, so the feeds can say so rather than go silent.</summary>
    public volatile bool Connected;
}
