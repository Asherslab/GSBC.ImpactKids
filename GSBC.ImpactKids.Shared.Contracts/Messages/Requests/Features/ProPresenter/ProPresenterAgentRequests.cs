using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;

/// <summary>
/// Deliberately empty. Every agent operation acts on the calling machine, which the server takes
/// from the token and never from a request - so there is nothing to put here, and protobuf-net
/// still wants a message type.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ProPresenterAgentRequest;

/// <summary>What the agent can see from where it sits. The server stamps the time; the agent's clock is not trusted for it.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ReportProPresenterAgentStatusRequest
{
    public required string  AgentVersion          { get; set; }
    public required bool    ProPresenterReachable { get; set; }
    public          string? ProPresenterVersion   { get; set; }

    /// <summary>The timers set up in ProPresenter right now. Empty when it could not be asked.</summary>
    public List<ProPresenterTimer> Timers { get; set; } = [];
}
