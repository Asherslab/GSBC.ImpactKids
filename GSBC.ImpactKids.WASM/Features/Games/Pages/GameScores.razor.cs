using System.Collections.Immutable;
using System.Globalization;
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

    /// <summary>
    /// Whether the numbers on this page read as the wall shows them or as they were
    /// tapped in. Display points are the default: they are what the room will see, and
    /// this page exists to check that before the reveal.
    /// </summary>
    private bool _showMultiplied = true;

    private Guid ServiceKey => _service.Data?.Id ?? Guid.Empty;

    private int GamesPlayed => Points.GamesPlayed(ServiceKey);

    private GameBoard Board => Points.BoardFor(ServiceKey);

    /// <summary>
    /// The games that are part of the night, in order - planned and voided ones left out.
    /// <para>
    /// Everything on this page is positional over this list, and so is the board the server
    /// streams to the wall. The reveal takes its step count from the length on both ends, so
    /// one end counting a game the other does not slides every later step out of place.
    /// </para>
    /// </summary>
    private IReadOnlyList<int> CountingGames => Board.CountingGames(GamesPlayed);

    /// <summary>
    /// A team's night, both ways up: what it scored in each game and what that reads as on
    /// screen. Held together so the page can flip between them without recounting, and so
    /// placings always come off the display totals however the page is being read.
    /// </summary>
    private sealed record TeamStanding(
        GameTeamDefinition Team,
        int[]              PerGame,
        int[]              PerGameDisplay,
        int                Behaviour,
        int                BehaviourDisplay,
        int                ScoredTotal,
        int                DisplayTotal
    );

    /// <summary>
    /// Competition placing: two teams level on points are both second, and the team after
    /// them is fourth. Handing one of them the silver on a tie is just wrong.
    /// <para>
    /// Always off the display totals, even while the page is showing scored points -
    /// per game multipliers mean the two can rank differently, and the wall is right.
    /// </para>
    /// </summary>
    private static int PlaceOf(TeamStanding standing, IReadOnlyList<TeamStanding> standings) =>
        standings.Count(x => x.DisplayTotal > standing.DisplayTotal) + 1;

    /// <summary>
    /// Teams level on points get the same label rather than an "=" marker - two golds
    /// says "tie" to anyone; "=🥇" needs explaining.
    /// </summary>
    private static string PlacingLabel(TeamStanding standing, IReadOnlyList<TeamStanding> standings) =>
        Placing(PlaceOf(standing, standings));

    private IReadOnlyList<TeamStanding> Standings
    {
        get
        {
            IReadOnlyList<int> games = CountingGames;

            int[] multipliers = Board.MultipliersThrough(GamesPlayed);
            int   behaviourMultiplier = Board.BehaviourMultiplier();

            return Board.EffectiveTeams()
                .Select(team =>
                    {
                        int[] perGame = games
                            .Select(game => Points.GamePointsFor(ServiceKey, team.Index, game))
                            .ToArray();

                        // Indexed by game number, not by position: the list above holds only
                        // the games that count, so the two no longer line up.
                        int[] perGameDisplay = games
                            .Select((game, index) => GameMultipliers.Multiply(
                                    perGame[index],
                                    multipliers[game - 1]
                                )
                            )
                            .ToArray();

                        int behaviour = Points.BehaviourPointsFor(ServiceKey, team.Index);

                        int behaviourDisplay = GameMultipliers.Multiply(behaviour, behaviourMultiplier);

                        return new TeamStanding(
                            team,
                            perGame,
                            perGameDisplay,
                            behaviour,
                            behaviourDisplay,
                            perGame.Sum() + behaviour,
                            perGameDisplay.Sum() + behaviourDisplay
                        );
                    }
                )
                .OrderByDescending(x => x.DisplayTotal)
                .ThenBy(x => x.Team.Index)
                .ToList();
        }
    }

    // ---------- reading the table ----------

    /// <summary>
    /// A team's haul in one game. <paramref name="position"/> is where the game sits in
    /// <see cref="CountingGames"/>, not its number - a night with a voided game in it has
    /// no column for that game at all.
    /// </summary>
    private int GameValue(TeamStanding standing, int position) =>
        _showMultiplied ? standing.PerGameDisplay[position] : standing.PerGame[position];

    private int BehaviourValue(TeamStanding standing) =>
        _showMultiplied ? standing.BehaviourDisplay : standing.Behaviour;

    private int TotalValue(TeamStanding standing) =>
        _showMultiplied ? standing.DisplayTotal : standing.ScoredTotal;

    private int GameTotal(IReadOnlyList<TeamStanding> standings, int position) =>
        standings.Sum(x => GameValue(x, position));

    private int BehaviourTotal(IReadOnlyList<TeamStanding> standings) =>
        standings.Sum(BehaviourValue);

    private int GrandTotal(IReadOnlyList<TeamStanding> standings) => standings.Sum(TotalValue);

    /// <summary>Grouped, because five figures of display points is unreadable otherwise.</summary>
    private static string Format(int points) => points.ToString("N0", CultureInfo.CurrentCulture);

    // ---------- reveal remote ----------

    /// <summary>
    /// The reveal as this page's own data sees it. The display works the same list out
    /// from the streamed board, so a step number means the same thing on both screens.
    /// <para>
    /// Display points, because that is what the stream carries. Placings decide how many
    /// podium steps there are, and per game multipliers can rank teams differently to the
    /// scored points - one end counting a tie the other does not would slide every step
    /// after it out of line.
    /// </para>
    /// </summary>
    private IReadOnlyList<GameReveal.RevealTeam> RevealTeams =>
        Standings
            .OrderBy(x => x.Team.Index)
            .Select(standing => new GameReveal.RevealTeam(
                    standing.Team.Index,
                    standing.Team.Name,
                    standing.Team.Colour,
                    standing.PerGameDisplay,
                    standing.BehaviourDisplay
                )
            )
            .ToList();

    /// <summary>
    /// Round titles for the reveal, over the games that count - matching the names the
    /// server puts in the streamed board, in the same order.
    /// </summary>
    private IReadOnlyList<string> GameNames =>
        CountingGames.Select(x => Board.GameAt(x).DisplayName()).ToList();

    private IReadOnlyList<GameReveal.RevealRound> RevealRounds => GameReveal.Rounds(GameNames, RevealTeams);

    private IReadOnlyList<GameReveal.RevealStep> RevealSteps
    {
        get
        {
            IReadOnlyList<GameReveal.RevealTeam> teams = RevealTeams;

            if (teams.Count == 0)
                return [];

            IReadOnlyList<GameReveal.RevealRound> rounds = GameReveal.Rounds(GameNames, teams);

            return GameReveal.Steps(rounds.Count, GameReveal.PodiumPlacings(teams, rounds));
        }
    }

    private bool RevealRunning => Board.RevealStep != null;

    private int RevealIndex =>
        RevealSteps.Count == 0 ? 0 : Math.Clamp(Board.RevealStep ?? 0, 0, RevealSteps.Count - 1);

    private string RevealStepLabel
    {
        get
        {
            IReadOnlyList<GameReveal.RevealStep> steps = RevealSteps;

            return steps.Count == 0
                ? "Nothing to reveal yet"
                : GameReveal.Describe(steps[RevealIndex], RevealRounds);
        }
    }

    private string RevealStatus
    {
        get
        {
            int total = RevealSteps.Count;

            if (total == 0)
                return "Score something first";

            return RevealRunning
                ? $"Playing — step {RevealIndex + 1} of {total}"
                : $"{RevealRounds.Count} rounds ready";
        }
    }

    private string RevealDisplayHref =>
        ServiceKey == Guid.Empty ? "/Display/Reveal" : $"/Display/Reveal/{ServiceKey}";

    private Task StartReveal() => SetRevealStep(0);

    private Task RestartReveal() => SetRevealStep(0);

    private Task NextRevealStep() => SetRevealStep(Math.Min(RevealIndex + 1, RevealSteps.Count - 1));

    private Task PreviousRevealStep() => SetRevealStep(Math.Max(RevealIndex - 1, 0));

    /// <summary>Null puts the display back to its standby screen.</summary>
    private Task EndReveal() => SetRevealStep(null);

    private Task SetRevealStep(int? step) =>
        Points.UpdateBoardAsync(ServiceKey, board => board with { RevealStep = step });

    /// <summary>"G3" normally, but a named game earns its name on the chip.</summary>
    private string GameLabel(int number)
    {
        GameDefinition game = Board.GameAt(number);

        return game.Name ?? $"G{number}";
    }

    private static string Placing(int place) => place switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => GameReveal.Ordinal(place)
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
