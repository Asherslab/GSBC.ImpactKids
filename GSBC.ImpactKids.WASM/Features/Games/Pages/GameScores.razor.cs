using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Games.Pages;

public partial class GameScores
{
    [SupplyParameterFromQuery]
    public Guid? ServiceId { get; set; }

    private AsyncData<Service> _service = AsyncData<Service>.NotAsked();

    private Guid ServiceKey => _service.Data?.Id ?? Guid.Empty;

    private int GamesPlayed => Points.GamesPlayed(ServiceKey);

    private GameBoard Board => Points.BoardFor(ServiceKey);

    /// <summary>A team's night: what it scored in each game, plus behaviour points.</summary>
    private sealed record TeamStanding(
        GameTeamDefinition Team,
        int[]              PerGame,
        int                Behaviour,
        int                Total
    );

    private IReadOnlyList<TeamStanding> Standings
    {
        get
        {
            int games = GamesPlayed;

            return Board.EffectiveTeams()
                .Select(team => new TeamStanding(
                        team,
                        Enumerable.Range(1, games)
                            .Select(game => Points.GamePointsFor(ServiceKey, team.Index, game))
                            .ToArray(),
                        Points.BehaviourPointsFor(ServiceKey, team.Index),
                        Points.TotalFor(ServiceKey, team.Index)
                    )
                )
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.Team.Index)
                .ToList();
        }
    }

    /// <summary>"G3" normally, but a named game earns its name on the chip.</summary>
    private string GameLabel(int number)
    {
        GameDefinition game = Board.GameAt(number);

        return game.Name ?? $"G{number}";
    }

    private static string Placing(int index) => index switch
    {
        0 => "🥇",
        1 => "🥈",
        2 => "🥉",
        _ => $"{index + 1}."
    };

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(ServicesStore, RetrieveService);
        Points.Changed += OnPointsChanged;

        RetrieveService();

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

    public override void Dispose()
    {
        Points.Changed -= OnPointsChanged;
        base.Dispose();
    }
}
