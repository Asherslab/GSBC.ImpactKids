using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.ProPresenter.Pages;

/// <summary>
/// The connected ProPresenter machines: their health, their settings, and revoking them.
/// <para>
/// <b>Online is worked out on this page's clock, not pushed.</b> The agent reports every 30 seconds,
/// but the server raises a change event only when something visible changes - a heartbeat that woke
/// every phone would have the whole team re-reading this table all night. So the page re-renders on
/// its own timer, and a machine that stops reporting turns offline here without any event at all.
/// </para>
/// <para>
/// <b>And it re-reads on its own timer too</b>, or a machine reporting steadily would turn offline
/// here 90 seconds after the page loaded: with no event, the "last seen" it judges by never moves on.
/// Only while this page is open, so it costs one read per open admin page, not per phone.
/// </para>
/// </summary>
public partial class Machines
{
    /// <summary>
    /// The newest app release always carries the agent built from the same commit - the release job
    /// uploads it into the release chart-releaser creates and marks Latest. See
    /// docs/work/2026-10-propresenter-agent.md for why there is no separate agent release.
    /// </summary>
    private const string AgentDownloadUrl =
        "https://github.com/Asherslab/GSBC.ImpactKids/releases/latest/download/impactkids-propresenter-agent-macos.zip";

    private static readonly TimeSpan StatusRedrawInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Matches the agent's report interval, so "last seen" is at most a minute old - inside
    /// <see cref="ProPresenterMachine.OnlineWindow"/> - while the machine is reporting.
    /// </summary>
    private static readonly TimeSpan StatusReloadInterval = TimeSpan.FromSeconds(30);

    private AsyncData<ImmutableList<ProPresenterMachine>> _machines = AsyncData<ImmutableList<ProPresenterMachine>>.NotAsked();

    private UpdateProPresenterMachineRequest? _editing;
    private string?                           _editingName;
    private ImmutableList<ProPresenterTimer>  _editingTimers = [];
    private ImmutableList<ProPresenterLibrary> _editingLibraries = [];
    private ImmutableList<ProPresenterWorkspace> _editingWorkspaces = [];
    private string?                           _editingActiveWorkspace;
    private bool                              _busy;
    private Timer?                            _redraw;
    private Timer?                            _reload;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(MachinesStore, RetrieveMachines);
        RetrieveMachines();

        _redraw = new Timer(_ => InvokeAsync(StateHasChanged), null, StatusRedrawInterval, StatusRedrawInterval);
        // RefreshEvent, not RefreshAll: RefreshAll would answer from its 30 minute cache.
        _reload = new Timer(_ => InvokeAsync(MachinesStore.RefreshEvent), null, StatusReloadInterval, StatusReloadInterval);

        await MachinesStore.RefreshAll();
    }

    private void RetrieveMachines()
    {
        _machines = MachinesStore.GetState().Entities;
        StateHasChanged();
    }

    private static (string Label, Color Color) Status(ProPresenterMachine machine)
    {
        if (!machine.Enrolled)
            return ("Never finished connecting", Color.Warning);

        return machine.IsOnline(DateTime.UtcNow)
            ? ("Online", Color.Success)
            : ("Offline", Color.Default);
    }

    private void StartEdit(ProPresenterMachine machine)
    {
        _editing = UpdateProPresenterMachineRequest.FromEntity(machine);
        _editingName = machine.Name;

        // Keep a chosen timer listed even if ProPresenter has since lost it, so saving does not silently clear it.
        _editingTimers = machine.ScheduleTimerUuid is { } chosen && machine.Timers.All(t => t.Uuid != chosen)
            ? machine.Timers.Add(new ProPresenterTimer { Uuid = chosen, Name = "(no longer in ProPresenter)" })
            : machine.Timers;

        // The same for libraries.
        _editingLibraries = machine.Libraries.AddRange(machine.SongLibraryUuids
            .Where(uuid => machine.Libraries.All(l => l.Uuid != uuid))
            .Select(uuid => new ProPresenterLibrary { Uuid = uuid, Name = "(no longer in ProPresenter)" }));

        // And for the workspace. Never filled in with the open one: saving a rename while ProPresenter
        // happened to be on the church's workspace would quietly start copying that.
        _editingWorkspaces = machine.SongWorkspaceUuid is { } workspace && machine.Workspaces.All(w => w.Uuid != workspace)
            ? machine.Workspaces.Add(new ProPresenterWorkspace { Uuid = workspace, Name = "(no longer in ProPresenter)" })
            : machine.Workspaces;
        _editingActiveWorkspace = machine.ActiveWorkspaceUuid;
    }

    private string? LibraryName(string? uuid) =>
        _editingLibraries.FirstOrDefault(l => l.Uuid == uuid)?.Name ?? uuid;

    private string? WorkspaceName(string? uuid) =>
        _editingWorkspaces.FirstOrDefault(w => w.Uuid == uuid)?.Name ?? uuid;

    private string WorkspaceHelperText =>
        _editingWorkspaces.Count == 0
            ? "No workspaces reported yet - is the tool up to date, and ProPresenter on the same Mac?"
            : "Songs are copied only while this workspace is open in ProPresenter"
              + (_editingActiveWorkspace == null ? "" : $" - {WorkspaceName(_editingActiveWorkspace)} is open now");

    /// <summary>Why the Mac is not copying songs right now, when a leader would expect it to.</summary>
    private static string? SongWorkspaceWarning(ProPresenterMachine machine) =>
        machine.SongWorkspaceUuid == null
            ? machine.SongLibraryUuids.IsEmpty ? null : "Choose a workspace to copy songs from"
            : machine.ActiveWorkspaceUuid == null
                ? null
                : machine.ActiveWorkspaceUuid != machine.SongWorkspaceUuid
                    ? $"Not copying songs: ProPresenter has {machine.WorkspaceName(machine.ActiveWorkspaceUuid)} open"
                    : null;

    private async Task SaveEdit()
    {
        if (_editing == null)
            return;

        _busy = true;

        try
        {
            BasicResponse response = await MachineService.Update(_editing);

            if (response.HasErrorOrNull())
            {
                Snackbar.AddErrorResponse(response);
                return;
            }

            _editing = null;
            await MachinesStore.RefreshEvent();
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task Revoke(ProPresenterMachine machine)
    {
        bool? confirmed = await DialogService.ShowMessageBoxAsync(
            "Disconnect this Mac?",
            $"{machine.Name} stops working straight away. To connect it again, restart the tool on the Mac and approve it again.",
            yesText: "Disconnect", cancelText: "Cancel");

        if (confirmed != true)
            return;

        BasicResponse response = await MachineService.BasicDelete(new BasicReadRequest { Guid = machine.Id });

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        if (_editing?.Id == machine.Id)
            _editing = null;

        await MachinesStore.RefreshEvent();
    }

    public override void Dispose()
    {
        _redraw?.Dispose();
        _reload?.Dispose();
        base.Dispose();
    }
}
