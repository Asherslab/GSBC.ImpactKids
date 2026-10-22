using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;

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

    /// <summary>ProPresenter's libraries right now. Empty when it could not be asked.</summary>
    public List<ProPresenterLibrary> Libraries { get; set; } = [];

    /// <summary>ProPresenter's workspaces on the Mac. Empty when they could not be read.</summary>
    public List<ProPresenterWorkspace> Workspaces { get; set; } = [];

    /// <summary>The workspace ProPresenter has open, whose libraries the API is showing. Null when it could not be read.</summary>
    public string? ActiveWorkspaceUuid { get; set; }
}

/// <summary>
/// <b>Every</b> song in the machine's chosen libraries, as ProPresenter has them now - not just the
/// changed ones. The server works out what changed, and a song this machine sent before that is
/// missing here has been deleted in ProPresenter. So the agent sends this only when it read every
/// library completely: a list cut short by ProPresenter closing would delete the rest.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SyncProPresenterSongsRequest
{
    /// <summary>
    /// The workspace the songs were read from, open in ProPresenter before and after the read. The
    /// server refuses the list unless it is the machine's chosen workspace - so an agent from before
    /// workspaces, which sends none, cannot empty the library either.
    /// </summary>
    public string? WorkspaceUuid { get; set; }

    public List<ProPresenterSong> Songs { get; set; } = [];
}

/// <summary>One song as the agent read it from ProPresenter's API.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ProPresenterSong
{
    /// <summary>ProPresenter's id for the presentation - the same on every Mac sharing a cloud workspace.</summary>
    public required string Uuid    { get; set; }
    public required string Name    { get; set; }
    public required string Library { get; set; }

    public List<SongGroup>       Groups       { get; set; } = [];
    public List<SongArrangement> Arrangements { get; set; } = [];

    public string? DefaultArrangementUuid { get; set; }
}
