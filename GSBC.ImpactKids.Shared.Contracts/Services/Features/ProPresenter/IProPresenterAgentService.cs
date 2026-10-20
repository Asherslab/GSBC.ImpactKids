using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.ProPresenter;

/// <summary>
/// What a ProPresenter machine may ask, and the <b>only</b> thing it may ask - a machine token
/// authenticates on nothing else in the service.
/// <para>
/// Routed under <c>machine/</c> rather than <c>gRPC/</c>, which is load bearing: the proxy's
/// <c>gRPC/</c> routes demand a browser cookie and overwrite the Authorization header, and the agent
/// has no cookie and brings its own token. See <c>docs/work/2026-10-propresenter-agent.md</c>.
/// </para>
/// <para>
/// Every operation acts on the calling machine, identified by its token. None takes a machine id,
/// so a machine cannot ask about another one by changing a field.
/// </para>
/// </summary>
[Service("machine/GSBC.ImpactKids.ProPresenter.Agent")]
public interface IProPresenterAgentService
{
    Task<BasicReadResponse<ProPresenterAgentConfiguration>> GetConfiguration(
        ProPresenterAgentRequest request,
        CallContext              context = default
    );

    Task<BasicResponse> ReportStatus(
        ReportProPresenterAgentStatusRequest request,
        CallContext                          context = default
    );

    /// <summary>
    /// Children signed in today whose first time at Impact Kids is today, both read as church dates (<see cref="ChurchTime"/>).
    /// First names only - see <see cref="FirstTimer"/>.
    /// </summary>
    Task<BasicReadMultipleResponse<FirstTimer>> GetFirstTimersToday(
        ProPresenterAgentRequest request,
        CallContext              context = default
    );

    /// <summary>Today's service's schedule, with what has run so far. Entity null when there is none tonight.</summary>
    Task<BasicReadResponse<ProPresenterTonight>> GetTonight(
        ProPresenterAgentRequest request,
        CallContext              context = default
    );

    /// <summary>
    /// Every song in the machine's chosen libraries, for the song library. Songs this machine sent
    /// before and has not sent now are marked removed - see <see cref="SyncProPresenterSongsRequest"/>.
    /// </summary>
    Task<BasicResponse> SyncSongs(
        SyncProPresenterSongsRequest request,
        CallContext                  context = default
    );
}
