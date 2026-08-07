using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Games.Pages;

public partial class PointsTracker
{
    /// <summary>Defaults to today's service, same as the attendance tool.</summary>
    [SupplyParameterFromQuery]
    public Guid? ServiceId { get; set; }

    private AsyncData<Service> _service = AsyncData<Service>.NotAsked();

    private bool _settingsOpen;

    private Guid ServiceKey => _service.Data?.Id ?? Guid.Empty;

    private GameBoard Board => Points.BoardFor(ServiceKey);

    private bool CanUndo => _service.Data != null && Points.CanUndo(ServiceKey);

    private string SyncLabel => !Points.IsOnline
        ? Points.PendingCount > 0
            ? $"Offline · {Points.PendingCount} queued"
            : "Offline"
        : Points.PendingCount > 0
            ? $"Syncing {Points.PendingCount}"
            : "Synced";

    private Color SyncColour => !Points.IsOnline
        ? Color.Warning
        : Points.PendingCount > 0
            ? Color.Info
            : Color.Success;

    private string SyncIcon => !Points.IsOnline
        ? Icons.Material.Filled.CloudOff
        : Points.PendingCount > 0
            ? Icons.Material.Filled.CloudSync
            : Icons.Material.Filled.CloudDone;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(ServicesStore, RetrieveService);
        Points.Changed += OnPointsChanged;

        RetrieveService();

        // The tracker must become usable from cached local state even if the
        // services call never comes back.
        await Points.InitialiseAsync();
        await ServicesStore.RefreshAll();
    }

    private void OnPointsChanged() => InvokeAsync(StateHasChanged);

    private void RetrieveService()
    {
        AsyncData<ImmutableList<Service>> services = ServicesStore.GetState().Entities;

        if (!services.HasData)
        {
            _service = _service.CopyStatus(services);
            StateHasChanged();
            return;
        }

        Service? service;

        if (ServiceId != null)
        {
            service = services.Data!
                .FirstOrDefault(x => x.Id == ServiceId);
        }
        else
        {
            service = services.Data!
                .FirstOrDefault(x => x.LocalDate.Date == DateTime.Today);

            service ??= services.Data!
                .OrderByDescending(x => x.LocalDate.Date)
                .FirstOrDefault();
        }

        _service = service != null
            ? _service.ToSuccess(service)
            : ServiceId == null
                ? _service.ToFailure("Failed to find Service for Today")
                : _service.ToFailure("Failed to find Service for Id");

        StateHasChanged();
    }

    // ---------- scoring ----------

    private async Task AddGamePoints(GameTeam team, int points)
    {
        if (_service.Data == null)
            return;

        await Points.AddGamePointsAsync(ServiceKey, team, points);
    }

    private async Task AddBehaviourPoints(GameTeam team, int points)
    {
        if (_service.Data == null)
            return;

        await Points.AddBehaviourPointsAsync(ServiceKey, team, points);
    }

    private async Task UndoLast()
    {
        if (_service.Data == null)
            return;

        await Points.UndoLastAsync(ServiceKey);
    }

    // ---------- board ----------

    private async Task StartNewGame()
    {
        if (_service.Data == null)
            return;

        await UpdateBoard(board => board with { CurrentGame = board.CurrentGame + 1 });

        Snackbar.Add($"Game {Board.CurrentGame} started", Severity.Success);
    }

    private Task TogglePaused() => UpdateBoard(board => board with
        {
            Paused = !board.Paused,
            // Freeze the display at the moment of pausing. Scoring carries on.
            PausedAt = board.Paused ? null : DateTime.UtcNow
        }
    );

    private Task ToggleHidden() => UpdateBoard(board => board with { Hidden = !board.Hidden });

    private Task SetDisplayMode(GameDisplayMode mode) => UpdateBoard(board => board with { DisplayMode = mode });

    private Task SetTeamCount(int teamCount) => UpdateBoard(board => board with { TeamCount = teamCount });

    private Task SetStepPoints(int points) => UpdateBoard(board => board with { StepPoints = points });

    private Task SetBonusPoints(int points) => UpdateBoard(board => board with { BonusPoints = points });

    private async Task UpdateBoard(Func<GameBoard, GameBoard> mutate)
    {
        if (_service.Data == null)
            return;

        await Points.UpdateBoardAsync(ServiceKey, mutate);
    }

    public override void Dispose()
    {
        Points.Changed -= OnPointsChanged;
        base.Dispose();
    }
}
