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
    private bool _teamsOpen;
    private bool _alliancesOpen;
    private bool _renameOpen;

    private string _gameNameDraft = string.Empty;

    /// <summary>Teams ticked in the combine dialog, waiting to be put on one side.</summary>
    private readonly HashSet<int> _allianceSelection = [];

    private Guid ServiceKey => _service.Data?.Id ?? Guid.Empty;

    private GameBoard Board => Points.BoardFor(ServiceKey);

    private ImmutableList<GameTeamDefinition> Teams => Board.EffectiveTeams();

    private GameDefinition CurrentGame => Board.CurrentGameDefinition();

    private int GamesPlayed => Points.GamesPlayed(ServiceKey);

    private bool CanUndo => _service.Data != null && Points.CanUndo(ServiceKey);

    private bool CanGoBack => Board.CurrentGame > 1;

    /// <summary>
    /// The game the forward arrow goes to: one already played, or one planned in advance
    /// and waiting. Null means this is the end of the night so far, and the arrow becomes
    /// the button that starts a new game.
    /// </summary>
    private int? NextGameNumber => Board.NextGameAfter(Board.CurrentGame, GamesPlayed);

    private bool CanGoForward => NextGameNumber != null;

    /// <summary>One scoring tile: a team, or the teams playing this game combined.</summary>
    private sealed record Side(ImmutableList<GameTeamDefinition> Teams)
    {
        public string Label => string.Join(" + ", Teams.Select(x => x.Name));

        public IReadOnlyList<int> Indexes => Teams.ConvertAll(x => x.Index);
    }

    /// <summary>
    /// The tiles to draw. Combined teams collapse into one tile - they are scored
    /// together, so two tiles showing the same number would only invite a double tap.
    /// </summary>
    private IReadOnlyList<Side> Sides
    {
        get
        {
            ImmutableList<GameTeamDefinition> teams = Teams;

            return GameAlliances
                .Groups(CurrentGame.Alliances, teams.Count)
                .Select(group => new Side(group.ConvertAll(index => teams[index])))
                .ToList();
        }
    }

    /// <summary>Past four tiles the grid stops trying to fill the screen and starts scrolling.</summary>
    private bool Compact => Sides.Count > 4;

    // ---------- placement ----------

    private bool IsPlacement => CurrentGame.IsPlacement();

    /// <summary>
    /// The finishing order being built, by side index, best first - a group with more
    /// than one side in it is a dead heat.
    /// <para>
    /// Held here rather than written as it goes, because a race is one award: the round
    /// lands on one group id, undoes in one tap, and cannot leave half a result behind if
    /// the phone is locked halfway down the field.
    /// </para>
    /// </summary>
    private ImmutableList<ImmutableList<int>> _order = GamePlacementOrder.Empty;

    /// <summary>
    /// The game the order in hand belongs to. Stepping to another game abandons it - a
    /// half placed race carried into the next game would be awarded to the wrong one.
    /// </summary>
    private int _orderGame;

    private bool _roundsOpen;

    private ImmutableList<ImmutableList<int>> Order
    {
        get
        {
            if (_orderGame != Board.CurrentGame)
            {
                _orderGame = Board.CurrentGame;
                _order = GamePlacementOrder.Empty;
            }

            return _order;
        }
    }

    private int PlacedCount => Order.Sum(group => group.Count);

    private IReadOnlyList<GameRound> Rounds => _service.Data == null
        ? []
        : Points.RoundsFor(ServiceKey, Board.CurrentGame);

    /// <summary>Where the given side is standing, or null while it is still waiting.</summary>
    private int? PlaceOfSide(int sideIndex) => GamePlacementOrder.PlaceFor(Order, sideIndex);

    private int NextPlace => GamePlacementOrder.NextPlace(Order);

    private int? TiePlace => GamePlacementOrder.TiePlace(Order);

    /// <summary>What a place pays in this game, in scored points.</summary>
    private int PointsForPlace(int place) => GamePlacements.PointsAt(CurrentGame.PlacementPoints, place);

    private void PlaceSide(int sideIndex)
    {
        _orderGame = Board.CurrentGame;
        _order = GamePlacementOrder.Toggle(Order, sideIndex);
    }

    private void TieSide(int sideIndex)
    {
        _orderGame = Board.CurrentGame;
        _order = GamePlacementOrder.Tie(Order, sideIndex);
    }

    private void ClearOrder() => _order = GamePlacementOrder.Empty;

    /// <summary>
    /// Turns the order in hand into one award per place. A combined side hands its points
    /// to every team in it, exactly as a tap does.
    /// </summary>
    private async Task AwardRound()
    {
        if (_service.Data == null || PlacedCount == 0)
            return;

        await EnsureCurrentGamePlayed();

        IReadOnlyList<Side> sides = Sides;

        List<GamePlacementAward> awards = [];

        for (int index = 0; index < Order.Count; index++)
        {
            int place = GamePlacementOrder.PlaceOf(Order, index);

            List<int> teams = Order[index]
                .Where(side => side >= 0 && side < sides.Count)
                .SelectMany(side => sides[side].Indexes)
                .Distinct()
                .ToList();

            if (teams.Count == 0)
                continue;

            awards.Add(new GamePlacementAward
                {
                    TeamIndexes = teams,
                    Place = place,
                    Points = PointsForPlace(place)
                }
            );
        }

        int number = Rounds.Count + 1;

        await Points.AwardPlacementAsync(ServiceKey, Board.CurrentGame, awards);

        ClearOrder();

        Snackbar.Add($"Round {number} awarded", Severity.Success);
    }

    private async Task DeleteRound(GameRound round)
    {
        if (_service.Data == null)
            return;

        await Points.DeleteRoundAsync(ServiceKey, round.Key);

        Snackbar.Add($"Round {round.Number} removed", Severity.Info);
    }

    /// <summary>
    /// Switches this game between tapping and placement. Placement is never inherited by
    /// the next game - a way of playing is not a rate - so turning it on writes this
    /// game's own values, sized to the sides playing it.
    /// </summary>
    private Task TogglePlacement()
    {
        ClearOrder();

        ImmutableList<int>? points = IsPlacement ? null : GamePlacements.Default(Sides.Count);

        return UpdateBoard(board => board.WithGame(
                board.CurrentGameDefinition() with { PlacementPoints = points }
            )
        );
    }

    private Task SetPlacementPreset(GamePlacements.PlacementPreset preset) =>
        UpdateBoard(board => board.WithGame(
                board.CurrentGameDefinition() with { PlacementPoints = preset.Build(Sides.Count) }
            )
        );

    /// <summary>"1st Red · 2nd Blue · 3rd Green" - a heat in the order it finished.</summary>
    private string RoundSummary(GameRound round)
    {
        ImmutableList<GameTeamDefinition> teams = Teams;

        IEnumerable<string> parts = round.Entries
            .OrderBy(entry => entry.Place)
            .ThenBy(entry => entry.TeamIndex)
            .Where(entry => entry.TeamIndex >= 0 && entry.TeamIndex < teams.Count)
            .Select(entry => $"{GamePlacements.Ordinal(entry.Place)} {teams[entry.TeamIndex].Name}");

        return string.Join(" · ", parts);
    }

    /// <summary>"1st 10 · 2nd 9 · 3rd 8" - what the game is paying, in scored points.</summary>
    private string PlacementSummary => CurrentGame.PlacementPoints == null
        ? string.Empty
        : string.Join(
            " · ",
            CurrentGame.PlacementPoints.Select((points, index) => $"{GamePlacements.Ordinal(index + 1)} {points}")
        );

    private string GridStyle
    {
        get
        {
            int count = Sides.Count;

            int columns        = count <= 2 ? 1 : 2;
            int wideColumns    = Math.Clamp(count, 1, count <= 4 ? 4 : 5);

            return $"--cols: {columns}; --cols-wide: {wideColumns};";
        }
    }

    private bool _resyncing;

    /// <summary>
    /// Tapping the sync chip forces the whole cycle: connectivity, send, re-read. It is
    /// the fix for the queue that says "Syncing 3" and stays there.
    /// </summary>
    private async Task Resync()
    {
        if (_resyncing)
            return;

        _resyncing = true;

        try
        {
            if (!Points.Initialised)
                await Points.InitialiseAsync();

            await Points.ResyncAsync();
        }
        catch
        {
            // Still offline, or the server is down. The queue is intact either way and
            // the chip goes back to saying so.
        }
        finally
        {
            _resyncing = false;
            StateHasChanged();
        }
    }

    private string SyncLabel => _resyncing
        ? "Checking…"
        : !Points.IsOnline
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

    private async Task AddSidePoints(Side side, int points)
    {
        if (_service.Data == null)
            return;

        await EnsureCurrentGamePlayed();

        await Points.AddGamePointsAsync(ServiceKey, side.Indexes, points);
    }

    /// <summary>
    /// A game being scored is being played. Opening a game already takes it off the planned
    /// list, but the game the page opens on was never navigated to - on a night planned
    /// from game 1, that is the first game of the night.
    /// </summary>
    private Task EnsureCurrentGamePlayed() =>
        CurrentGame.Planned
            ? UpdateBoard(board => board.WithGame(board.CurrentGameDefinition() with { Planned = false }))
            : Task.CompletedTask;

    private async Task AddBehaviourPoints(int teamIndex, int points)
    {
        if (_service.Data == null)
            return;

        await Points.AddBehaviourPointsAsync(ServiceKey, teamIndex, points);
    }

    private async Task UndoLast()
    {
        if (_service.Data == null)
            return;

        await Points.UndoLastAsync(ServiceKey);
    }

    /// <summary>Each team's own running total - a combined tile hides them behind one number.</summary>
    private string? SideSubtitle(Side side) =>
        side.Teams.Count < 2
            ? null
            : string.Join(" · ", side.Teams.Select(x => $"{x.Name} {Points.TotalFor(ServiceKey, x.Index)}"));

    private int SidePoints(Side side, Func<int, int> pointsFor) =>
        side.Teams.Count == 0 ? 0 : pointsFor(side.Teams[0].Index);

    // ---------- games ----------

    private async Task StartNewGame()
    {
        if (_service.Data == null)
            return;

        // A night planned out in the hall is played in the order it was planned, so "new
        // game" picks up the next game waiting rather than opening a blank one after it.
        int? planned = Board.NextPlannedGame(Board.CurrentGame);

        // Always lands after the last game played, even if we had stepped back to look
        // at an earlier one.
        int next = planned ?? Math.Max(Board.CurrentGame, GamesPlayed) + 1;

        await GoToGameNumber(next);

        Snackbar.Add(
            planned == null
                ? $"Game {next} started"
                : $"{Board.GameAt(next).DisplayName()} started",
            Severity.Success
        );
    }

    /// <summary>
    /// Opens a game, and takes it off the planned list if it was on it - a game somebody
    /// is scoring is being played, whatever it was set up as. Nothing to remember to press.
    /// </summary>
    private Task GoToGameNumber(int number) =>
        UpdateBoard(board =>
            {
                GameBoard moved = board with { CurrentGame = number };

                GameDefinition definition = moved.GameAt(number);

                return definition.Planned
                    ? moved.WithGame(definition with { Planned = false })
                    : moved;
            }
        );

    /// <summary>
    /// Steps back a game. A game nobody scored in never really happened, so it does not
    /// get to leave an empty column on the tally - but what happens to it depends on
    /// whether anybody ever said anything about it.
    /// <para>
    /// A game opened by accident is nothing but its number, and is forgotten. A game that
    /// was set up - named, priced, made a race, or planned in the hall - is put back on the
    /// planned list instead. Binning it took the whole night's preparation with it, which
    /// is exactly what stepping forward and back again used to do.
    /// </para>
    /// </summary>
    private async Task PreviousGame()
    {
        if (_service.Data == null || !CanGoBack)
            return;

        int leaving = Board.CurrentGame;

        GameDefinition definition = Board.GameAt(leaving);

        bool unscored = leaving >= GamesPlayed && !Points.HasScores(ServiceKey, leaving);

        bool discard = unscored && !definition.HasSettings();

        // Not for a voided game: hidden is somebody's decision and outranks this.
        bool replan = unscored && definition.HasSettings() && !definition.Hidden;

        await UpdateBoard(board =>
            {
                GameBoard moved = board with { CurrentGame = leaving - 1 };

                if (discard)
                    return moved with { Games = moved.Games.RemoveAll(x => x.Number == leaving) };

                return replan
                    ? moved.WithGame(moved.GameAt(leaving) with { Planned = true })
                    : moved;
            }
        );

        if (discard)
            Snackbar.Add($"Game {leaving} discarded - nothing was scored", Severity.Info);
        else if (replan)
            Snackbar.Add($"{definition.DisplayName()} is waiting again - nothing was scored", Severity.Info);
    }

    private Task NextGame() => NextGameNumber is { } next
        ? GoToGameNumber(next)
        : Task.CompletedTask;

    /// <summary>Games to offer jumping to: played, or set up ahead and waiting.</summary>
    private IReadOnlyList<int> SelectableGames =>
        [..Enumerable
            .Range(1, Math.Max(GamesPlayed, Board.HighestDefinedGame()))
            .Where(number => number <= GamesPlayed || Board.Games.Any(x => x.Number == number))
            .Where(number => !Board.GameAt(number).Hidden || number == Board.CurrentGame)
        ];

    private Task GoToGame(int number) =>
        GoToGameNumber(Math.Clamp(number, 1, Math.Max(GamesPlayed, Board.HighestDefinedGame())));

    private void OpenRenameGame()
    {
        _gameNameDraft = CurrentGame.Name ?? string.Empty;
        _renameOpen = true;
    }

    private async Task SaveGameName()
    {
        _renameOpen = false;

        string? name = string.IsNullOrWhiteSpace(_gameNameDraft) ? null : _gameNameDraft.Trim();

        await UpdateBoard(board => board.WithGame(board.CurrentGameDefinition() with { Name = name }));
    }

    // ---------- alliances ----------

    private void OpenAlliances()
    {
        _allianceSelection.Clear();
        _alliancesOpen = true;
    }

    private void ToggleAllianceSelection(int teamIndex)
    {
        if (!_allianceSelection.Add(teamIndex))
            _allianceSelection.Remove(teamIndex);
    }

    private Task CombineSelected()
    {
        if (_allianceSelection.Count < 2)
            return Task.CompletedTask;

        int[] selected = [.._allianceSelection];

        _allianceSelection.Clear();

        return SetAlliances(current => GameAlliances.Combine(current, Teams.Count, selected));
    }

    private Task SeparateGroup(ImmutableList<int> group) =>
        SetAlliances(current => GameAlliances.Separate(current, Teams.Count, group));

    private Task PairUpTeams() => SetAlliances(_ => GameAlliances.PairUp(Teams.Count));

    private Task ClearAlliances() => SetAlliances(_ => GameAlliances.None);

    private Task SetAlliances(Func<ImmutableList<int>, ImmutableList<int>> mutate)
    {
        // Combining teams renumbers the sides, so anything already placed is stale.
        ClearOrder();

        return UpdateBoard(board => board.WithGame(
                board.CurrentGameDefinition() with { Alliances = mutate(board.CurrentGameDefinition().Alliances) }
            )
        );
    }

    // ---------- teams ----------

    private Task SetTeamCount(int count)
    {
        // Sides are positional too, so a part placed round no longer points at the teams
        // the leader thought it did.
        ClearOrder();

        return UpdateBoard(board => board with
            {
                Teams = GameTeams.Resize(board.EffectiveTeams(), count),

                // Alliances are positional, so changing the team list makes every grouping
                // stale. Names, multipliers and placement points survive; the combining
                // has to be redone.
                Games = board.Games
                    .Select(game => game with { Alliances = [] })
                    .Where(game => !string.IsNullOrWhiteSpace(game.Name)
                                   || game.Multiplier != null
                                   || game.IsPlacement()
                    )
                    .ToImmutableList()
            }
        );
    }

    private Task RenameTeam(int index, string? name) =>
        UpdateBoard(board => board with { Teams = GameTeams.Rename(board.EffectiveTeams(), index, name) });

    private Task ShuffleTeamColour(int index) =>
        UpdateBoard(board => board with { Teams = GameTeams.ShuffleColour(board.EffectiveTeams(), index) });

    /// <summary>From the swatch's native colour picker, for when a team has to be a exact shade.</summary>
    private Task SetTeamColour(int index, string? colour) =>
        UpdateBoard(board => board with { Teams = GameTeams.SetColour(board.EffectiveTeams(), index, colour) });

    // ---------- board ----------

    private Task TogglePaused() => UpdateBoard(board => board with
        {
            Paused = !board.Paused,
            // Freeze the display at the moment of pausing. Scoring carries on.
            PausedAt = board.Paused ? null : DateTime.UtcNow
        }
    );

    private Task ToggleHidden() => UpdateBoard(board => board with { Hidden = !board.Hidden });

    private Task SetDisplayMode(GameDisplayMode mode) => UpdateBoard(board => board with { DisplayMode = mode });

    private Task SetStepPoints(int points) => UpdateBoard(board => board with { StepPoints = points });

    private Task SetBonusPoints(int points) => UpdateBoard(board => board with { BonusPoints = points });

    // ---------- display multiplier ----------

    /// <summary>What one point in the current game is worth on the wall.</summary>
    private string EffectiveMultiplier => $"×{Board.MultiplierFor(Board.CurrentGame)}";

    /// <summary>
    /// What this game would run at with no multiplier of its own - the game before it, or
    /// the night's. Shown as the placeholder so leaving the field empty is an obvious
    /// choice rather than a blank.
    /// </summary>
    private int InheritedMultiplier => Board.CurrentGame <= 1
        ? GameMultipliers.Normalise(Board.PointsMultiplier)
        : Board.MultiplierFor(Board.CurrentGame - 1);

    private Task SetBehaviourMultiplier(int multiplier) =>
        UpdateBoard(board => board with
            {
                BehaviourPointsMultiplier = GameMultipliers.Normalise(multiplier)
            }
        );

    private Task SetPointsMultiplier(int multiplier) =>
        UpdateBoard(board => board with { PointsMultiplier = GameMultipliers.Normalise(multiplier) });

    /// <summary>
    /// Null clears the override, which puts the game back to following the one before it.
    /// </summary>
    private Task SetCurrentGameMultiplier(int? multiplier) =>
        UpdateBoard(board => board.WithGame(
                board.CurrentGameDefinition() with { Multiplier = GameMultipliers.Normalise(multiplier) }
            )
        );

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
