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

    private AsyncData<ImmutableList<ProPresenterMachine>> _machines = AsyncData<ImmutableList<ProPresenterMachine>>.NotAsked();

    private UpdateProPresenterMachineRequest? _editing;
    private string?                           _editingName;
    private ImmutableList<ProPresenterTimer>  _editingTimers = [];
    private bool                              _busy;
    private Timer?                            _redraw;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(MachinesStore, RetrieveMachines);
        RetrieveMachines();

        _redraw = new Timer(_ => InvokeAsync(StateHasChanged), null, StatusRedrawInterval, StatusRedrawInterval);

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
    }

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
        base.Dispose();
    }
}
