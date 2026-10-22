using System.Text.Json;
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
            .Select(x => new
            {
                x.LastSeenAt, x.AgentVersion, x.ProPresenterReachable, x.ProPresenterVersion, x.TimersJson, x.LibrariesJson,
                x.WorkspacesJson, x.ActiveWorkspaceUuid
            })
            .FirstOrDefaultAsync(token);

        if (before == null)
            return BasicResponse.WithError(ProPresenterMachineNotFound);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Bounded, because the agent is a caller we do not fully trust to be sensible.
        string  agentVersion        = Truncate(request.AgentVersion, 64)!;
        string? proPresenterVersion = Truncate(request.ProPresenterVersion, 64);

        // ProPresenter unreachable reports no timers; keep the last list rather than empty the
        // admin page's choices because ProPresenter was closed for a moment.
        string? timersJson = request.ProPresenterReachable
            ? JsonSerializer.Serialize(request.Timers
                .Take(100)
                .Select(t => new ProPresenterTimer { Uuid = Truncate(t.Uuid, 64)!, Name = Truncate(t.Name, 200)! })
                .ToList())
            : before.TimersJson;

        // The same for libraries - and more so, because the libraries decide which songs are copied.
        string? librariesJson = request.ProPresenterReachable
            ? JsonSerializer.Serialize(request.Libraries
                .Take(100)
                .Select(l => new ProPresenterLibrary { Uuid = Truncate(l.Uuid, 64)!, Name = Truncate(l.Name, 200)! })
                .ToList())
            : before.LibrariesJson;

        // Read from the Mac's disk, not ProPresenter's API, so an empty list is an unreadable registry -
        // or an agent from before workspaces - and the last list stands. The open one is reported as
        // it is: null is "cannot tell", which the page says rather than show a stale workspace.
        string? workspacesJson = request.Workspaces.Count > 0
            ? JsonSerializer.Serialize(request.Workspaces
                .Take(100)
                .Select(w => new ProPresenterWorkspace { Uuid = Truncate(w.Uuid, 64)!, Name = Truncate(w.Name, 200)! })
                .ToList())
            : before.WorkspacesJson;

        string? activeWorkspaceUuid = Truncate(request.ActiveWorkspaceUuid, 64);

        await db.ProPresenterMachines
            .Where(x => x.Id == machineId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastSeenAt, now)
                .SetProperty(x => x.AgentVersion, agentVersion)
                .SetProperty(x => x.ProPresenterReachable, request.ProPresenterReachable)
                .SetProperty(x => x.ProPresenterVersion, proPresenterVersion)
                .SetProperty(x => x.TimersJson, timersJson)
                .SetProperty(x => x.LibrariesJson, librariesJson)
                .SetProperty(x => x.WorkspacesJson, workspacesJson)
                .SetProperty(x => x.ActiveWorkspaceUuid, activeWorkspaceUuid), token);

        bool wasOffline = before.LastSeenAt == null || now - before.LastSeenAt.Value >= ProPresenterMachine.OnlineWindow;

        bool visibleChange = wasOffline
                             || before.AgentVersion != agentVersion
                             || before.ProPresenterReachable != request.ProPresenterReachable
                             || before.ProPresenterVersion != proPresenterVersion
                             || before.TimersJson != timersJson
                             || before.LibrariesJson != librariesJson
                             || before.WorkspacesJson != workspacesJson
                             || before.ActiveWorkspaceUuid != activeWorkspaceUuid;

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
