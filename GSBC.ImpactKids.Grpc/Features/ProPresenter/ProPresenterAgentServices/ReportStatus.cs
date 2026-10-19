using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.ProPresenterAgentServices;

public partial class ProPresenterAgentService
{
    /// <summary>
    /// The agent's heartbeat, every 30 seconds. Writes only the status columns - the leader owns the
    /// rest of the row and may be editing it at the same moment.
    /// <para>
    /// <b>The change event is sent only when something a leader would see changes</b>, never for the
    /// timestamp alone. Every event makes every connected phone re-read the list, and a heartbeat that
    /// did that would have the whole leadership team polling the machines table all night. The admin
    /// page ages "last seen" on its own clock instead.
    /// </para>
    /// </summary>
    public async Task<BasicResponse> ReportStatus(
        ReportProPresenterAgentStatusRequest request,
        CallContext                          context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        Guid? machineId = CallingMachine(context);

        if (machineId == null)
            return BasicResponse.WithError(PermissionDenied);

        var before = await db.ProPresenterMachines
            .AsNoTracking()
            .Where(x => x.Id == machineId)
            .Select(x => new { x.LastSeenAt, x.AgentVersion, x.ProPresenterReachable, x.ProPresenterVersion })
            .FirstOrDefaultAsync(token);

        if (before == null)
            return BasicResponse.WithError(ProPresenterMachineNotFound);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Bounded, because the agent is a caller we do not fully trust to be sensible.
        string  agentVersion        = Truncate(request.AgentVersion, 64)!;
        string? proPresenterVersion = Truncate(request.ProPresenterVersion, 64);

        await db.ProPresenterMachines
            .Where(x => x.Id == machineId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastSeenAt, now)
                .SetProperty(x => x.AgentVersion, agentVersion)
                .SetProperty(x => x.ProPresenterReachable, request.ProPresenterReachable)
                .SetProperty(x => x.ProPresenterVersion, proPresenterVersion), token);

        bool wasOffline = before.LastSeenAt == null || now - before.LastSeenAt.Value >= ProPresenterMachine.OnlineWindow;

        bool visibleChange = wasOffline
                             || before.AgentVersion != agentVersion
                             || before.ProPresenterReachable != request.ProPresenterReachable
                             || before.ProPresenterVersion != proPresenterVersion;

        if (visibleChange)
            await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }

    private static string? Truncate(string? value, int length) =>
        value == null || value.Length <= length ? value : value[..length];
}
