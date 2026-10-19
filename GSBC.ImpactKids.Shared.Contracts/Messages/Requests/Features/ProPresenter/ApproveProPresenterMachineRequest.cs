namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;

/// <summary>
/// A leader approving the agent that opened the connect page. The last three fields arrive on that
/// page's query string, put there by the agent - see <c>docs/work/2026-10-propresenter-agent.md</c>.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ApproveProPresenterMachineRequest
{
    public required string Name { get; set; }

    /// <summary>Must be a loopback address. Anything else is refused on the server, not just in the page.</summary>
    public required string RedirectUri { get; set; }

    public required string CodeChallenge       { get; set; }
    public required string CodeChallengeMethod { get; set; }
}
