using System.Collections.Immutable;
using System.Globalization;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Games.Components;

/// <summary>
/// How the night is put together: the multipliers, the teams, and every game with
/// everything about it in one card.
/// <para>
/// Its own component because a game used to be edited in two places at once - name and
/// multiplier in one panel, placings and heats in another - and neither could add a game,
/// void one, or fix a score that went in wrong. Everything here is in <b>scored</b>
/// points, the units a leader taps in; the tally beside it is where the screen numbers
/// are checked.
/// </para>
/// </summary>
public partial class GameSetup : IDisposable
{
    /// <summary>The service being set up. Resolved by the page, so this is never empty.</summary>
    [Parameter]
    public required Guid ServiceId { get; set; }

    private GameBoard Board => Points.BoardFor(ServiceId);

    private ImmutableList<GameTeamDefinition> Teams => Board.EffectiveTeams();

    private int GamesPlayed => Points.GamesPlayed(ServiceId);

    /// <summary>
    /// Every game the board knows about - played, planned or voided. Wider than the tally's
    /// columns on purpose: a game planned for later exists here long before it appears
    /// anywhere else.
    /// </summary>
    private IReadOnlyList<int> SetupGames =>
        [..Enumerable.Range(1, Math.Max(GamesPlayed, Board.HighestDefinedGame()))];

    private IReadOnlyList<GameRound> RoundsOf(int game) => Points.RoundsFor(ServiceId, game);

    private int ScoredIn(int game, int teamIndex) => Points.GamePointsFor(ServiceId, teamIndex, game);

    private static string Format(int points) => points.ToString("N0", CultureInfo.CurrentCulture);

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Points.Changed += OnPointsChanged;
    }

    private void OnPointsChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Points.Changed -= OnPointsChanged;

    // ---------- the night ----------

    private Task SetPointsMultiplier(int multiplier) =>
        Points.UpdateBoardAsync(ServiceId, board => board with
            {
                PointsMultiplier = GameMultipliers.Normalise(multiplier)
            }
        );

    private Task SetBehaviourMultiplier(int multiplier) =>
        Points.UpdateBoardAsync(ServiceId, board => board with
            {
                BehaviourPointsMultiplier = GameMultipliers.Normalise(multiplier)
            }
        );

    // ---------- teams ----------

    private Task SetTeamCount(int count) =>
        Points.UpdateBoardAsync(ServiceId, board => board with
            {
                Teams = GameTeams.Resize(board.EffectiveTeams(), count),

                // Alliances are positional, so changing the team list makes every grouping
                // stale. Everything else about a game survives.
                Games = board.Games
                    .Select(game => game with { Alliances = [] })
                    .Where(game => !string.IsNullOrWhiteSpace(game.Name)
                                   || game.Multiplier != null
                                   || game.IsPlacement()
                                   || game.Planned
                                   || game.Hidden
                    )
                    .ToImmutableList()
            }
        );

    private Task RenameTeam(int index, string? name) =>
        Points.UpdateBoardAsync(ServiceId, board => board with
            {
                Teams = GameTeams.Rename(board.EffectiveTeams(), index, name)
            }
        );

    private Task SetTeamColour(int index, string? colour) =>
        Points.UpdateBoardAsync(ServiceId, board => board with
            {
                Teams = GameTeams.SetColour(board.EffectiveTeams(), index, colour)
            }
        );

    private Task ShuffleTeamColour(int index) =>
        Points.UpdateBoardAsync(ServiceId, board => board with
            {
                Teams = GameTeams.ShuffleColour(board.EffectiveTeams(), index)
            }
        );

    // ---------- games ----------

    /// <summary>
    /// What a game runs at with no multiplier of its own: the game before it, or the
    /// night's for game 1.
    /// </summary>
    private int InheritedMultiplier(int gameNumber) => gameNumber <= 1
        ? GameMultipliers.Normalise(Board.PointsMultiplier)
        : Board.MultiplierFor(gameNumber - 1);

    private Task SetGameName(int gameNumber, string? name) =>
        Points.UpdateBoardAsync(ServiceId, board => board.WithGame(
                board.GameAt(gameNumber) with
                {
                    Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim()
                }
            )
        );

    /// <summary>Cleared puts the game back to following the one before it.</summary>
    private Task SetGameMultiplier(int gameNumber, int? multiplier) =>
        Points.UpdateBoardAsync(ServiceId, board => board.WithGame(
                board.GameAt(gameNumber) with { Multiplier = GameMultipliers.Normalise(multiplier) }
            )
        );

    /// <summary>
    /// Adds a game after everything the board knows about, planned rather than started, so
    /// a night can be laid out in the hall without eight empty columns landing on the wall.
    /// </summary>
    private async Task AddGame()
    {
        int number = Board.NextGameNumber(GamesPlayed);

        await Points.UpdateBoardAsync(ServiceId, board => board.WithGame(
                board.GameAt(number) with { Planned = true }
            )
        );

        Snackbar.Add($"Game {number} added — it appears once it is scored", Severity.Success);
    }

    /// <summary>
    /// Voids a game, or puts it back. Its points are left alone either way: un-hiding has
    /// to return the game exactly as it was, or hiding would be a destructive act dressed
    /// up as a display setting.
    /// </summary>
    private async Task ToggleHidden(int game)
    {
        GameDefinition definition = Board.GameAt(game);

        bool hidden = !definition.Hidden;

        await Points.UpdateBoardAsync(ServiceId, board => board.WithGame(
                board.GameAt(game) with
                {
                    Hidden = hidden,

                    // Hiding something planned is the same decision twice over, and would
                    // leave it hidden for ever once it was finally played.
                    Planned = hidden ? false : definition.Planned
                }
            )
        );

        Snackbar.Add(
            hidden
                ? $"{definition.DisplayName()} hidden — it no longer counts"
                : $"{definition.DisplayName()} counts again",
            Severity.Info
        );
    }

    private bool _deleteOpen;

    private int _deleteGame;

    private void AskDelete(int game)
    {
        _deleteGame = game;
        _deleteOpen = true;
    }

    /// <summary>
    /// Drops a game and everything scored in it. The numbers of the games after it are
    /// left alone - every point record names its game, and renumbering would quietly move
    /// a phone's queued taps into the wrong game.
    /// </summary>
    private async Task DeleteGame()
    {
        _deleteOpen = false;

        int game = _deleteGame;

        string name = Board.GameAt(game).DisplayName();

        await Points.ClearGameAsync(ServiceId, game);

        await Points.UpdateBoardAsync(ServiceId, board =>
            {
                GameBoard cleared = board with
                {
                    Games = board.Games.RemoveAll(x => x.Number == game)
                };

                // A game deleted from the middle of the night would come straight back as
                // an empty column, because the night still reaches past it. Hiding it is
                // what makes it stay gone.
                return game < cleared.CurrentGame
                    ? cleared.WithGame(cleared.GameAt(game) with { Hidden = true })
                    : cleared;
            }
        );

        Snackbar.Add($"{name} deleted", Severity.Info);
    }

    // ---------- scores for a tapped game ----------

    private Task SetScore(int game, int teamIndex, int points) =>
        Points.SetGamePointsAsync(ServiceId, teamIndex, game, Math.Max(0, points));

    private Task NudgeScore(int game, int teamIndex, int by) =>
        SetScore(game, teamIndex, ScoredIn(game, teamIndex) + by);

    // ---------- placement ----------

    private int PlacesIn(int game) => Board.GameAt(game).PlacementPoints?.Count ?? 0;

    private int PlacementValue(int game, int place) =>
        GamePlacements.PointsAt(Board.GameAt(game).PlacementPoints, place);

    private Task TogglePlacement(int game)
    {
        GameDefinition definition = Board.GameAt(game);

        ImmutableList<int>? points = definition.IsPlacement()
            ? null
            : GamePlacements.Default(Teams.Count);

        return Points.UpdateBoardAsync(ServiceId, board => board.WithGame(
                board.GameAt(game) with { PlacementPoints = points }
            )
        );
    }

    private Task SetPlacementPreset(int game, GamePlacements.PlacementPreset preset) =>
        SetPlacementPoints(game, preset.Build(Teams.Count));

    /// <summary>One place up or down. A stepper rather than a field - nobody types here.</summary>
    private Task NudgePlacementValue(int game, int place, int by)
    {
        ImmutableList<int> points = Board.GameAt(game).PlacementPoints ?? [];

        if (place < 1 || place > points.Count)
            return Task.CompletedTask;

        int updated = Math.Clamp(points[place - 1] + by, GamePlacements.MinPoints, GamePlacements.MaxPoints);

        return SetPlacementPoints(game, points.SetItem(place - 1, updated));
    }

    /// <summary>
    /// Adds a place worth one less than the last, which is what "and the next one" means
    /// almost every time it is asked for.
    /// </summary>
    private Task AddPlace(int game)
    {
        ImmutableList<int> points = Board.GameAt(game).PlacementPoints ?? [];

        if (points.Count >= GamePlacements.MaxPlaces)
            return Task.CompletedTask;

        int next = points.Count == 0
            ? GamePlacements.DefaultTop
            : Math.Max(GamePlacements.MinPoints, points[^1] - 1);

        return SetPlacementPoints(game, points.Add(next));
    }

    /// <summary>
    /// Drops the last place. The field is not shortened by it - a place past the end of the
    /// list simply scores nothing, which is how "only the top three count" is said.
    /// </summary>
    private Task RemovePlace(int game)
    {
        ImmutableList<int> points = Board.GameAt(game).PlacementPoints ?? [];

        return points.Count <= 1
            ? Task.CompletedTask
            : SetPlacementPoints(game, points.RemoveAt(points.Count - 1));
    }

    /// <summary>
    /// Writes the new values and re-prices every heat already scored in that game.
    /// <para>
    /// Deciding after the fact that the race should have been worth more is the whole
    /// reason this editor exists, and a leader who changes what first place pays means the
    /// round they just watched. A round that had a score set by hand loses it here - the
    /// placings are what is being re-priced.
    /// </para>
    /// </summary>
    private async Task SetPlacementPoints(int game, ImmutableList<int> points)
    {
        ImmutableList<int>? normalised = GamePlacements.Normalise(points);

        await Points.UpdateBoardAsync(ServiceId, board => board.WithGame(
                board.GameAt(game) with { PlacementPoints = normalised }
            )
        );

        foreach (GameRound round in RoundsOf(game))
        {
            ImmutableList<ImmutableList<int>> order = round.Order();

            List<GamePlacementAward> awards = order
                .Select((group, index) =>
                    {
                        int place = GamePlacementOrder.PlaceOf(order, index);

                        return new GamePlacementAward
                        {
                            TeamIndexes = group,
                            Place = place,
                            Points = GamePlacements.PointsAt(normalised, place)
                        };
                    }
                )
                .ToList();

            await Points.ReplaceRoundAsync(ServiceId, game, round.Key, awards);
        }
    }

    // ---------- the round editor ----------

    private bool _roundEditorOpen;

    private int _editGame;

    /// <summary>The round being rewritten, or null when a new one is being added by hand.</summary>
    private Guid? _editRoundKey;

    private int _editRoundNumber;

    private ImmutableList<ImmutableList<int>> _editOrder = GamePlacementOrder.Empty;

    /// <summary>
    /// Scores set by hand for this round, by place. Empty is the normal case - a place is
    /// worth what the game says it is worth, and this is only for the round that differs.
    /// </summary>
    private readonly Dictionary<int, int> _editPoints = [];

    private string EditTitle
    {
        get
        {
            string game = Board.GameAt(_editGame).DisplayName();

            return _editRoundKey == null ? $"{game} — new round" : $"{game} — round {_editRoundNumber}";
        }
    }

    private void OpenRound(int game, GameRound? round)
    {
        _editGame = game;
        _editRoundKey = round?.Key;
        _editRoundNumber = round?.Number ?? RoundsOf(game).Count + 1;
        _editOrder = round?.Order() ?? GamePlacementOrder.Empty;

        _editPoints.Clear();

        // Start from what was actually awarded, so opening and saving changes nothing.
        if (round != null)
        {
            foreach (GameRoundEntry entry in round.Entries)
                _editPoints[entry.Place] = entry.Points;
        }

        _roundEditorOpen = true;
    }

    private void EditPlace(int teamIndex) => _editOrder = GamePlacementOrder.Toggle(_editOrder, teamIndex);

    private void EditTie(int teamIndex) => _editOrder = GamePlacementOrder.Tie(_editOrder, teamIndex);

    private void EditRemove(int teamIndex) => _editOrder = GamePlacementOrder.Remove(_editOrder, teamIndex);

    /// <summary>Puts a team in with the group above it, turning that place into a tie.</summary>
    private void EditTieWithAbove(int groupIndex, int teamIndex)
    {
        if (groupIndex <= 0)
            return;

        int place = GamePlacementOrder.PlaceOf(_editOrder, groupIndex - 1);

        _editOrder = GamePlacementOrder.TieWithPlace(_editOrder, teamIndex, place);
    }

    /// <summary>Takes a team out of a tie and gives it the place of its own just below.</summary>
    private void EditSplit(int groupIndex, int teamIndex)
    {
        int place = GamePlacementOrder.PlaceOf(_editOrder, groupIndex) + 1;

        _editOrder = GamePlacementOrder.MoveToPlace(_editOrder, teamIndex, place);
    }

    private int EditPointsFor(int place) =>
        _editPoints.TryGetValue(place, out int points) ? points : PlacementValue(_editGame, place);

    private void NudgeEditPoints(int place, int by) =>
        _editPoints[place] = Math.Clamp(
            EditPointsFor(place) + by,
            GamePlacements.MinPoints,
            GamePlacements.MaxPoints
        );

    private IReadOnlyList<GameTeamDefinition> EditUnplaced =>
        Teams
            .Where(team => GamePlacementOrder.GroupOf(_editOrder, team.Index) < 0)
            .ToList();

    private async Task SaveRound()
    {
        _roundEditorOpen = false;

        List<GamePlacementAward> awards = [];

        for (int index = 0; index < _editOrder.Count; index++)
        {
            int place = GamePlacementOrder.PlaceOf(_editOrder, index);

            awards.Add(new GamePlacementAward
                {
                    TeamIndexes = _editOrder[index],
                    Place = place,
                    Points = EditPointsFor(place)
                }
            );
        }

        if (_editRoundKey == null)
        {
            if (awards.Count > 0)
                await Points.AwardPlacementAsync(ServiceId, _editGame, awards);

            return;
        }

        // Everything taken out of the order is a team that did not run, so an emptied round
        // is a deleted one rather than a round with nobody in it.
        if (awards.Count == 0)
        {
            await Points.DeleteRoundAsync(ServiceId, _editRoundKey.Value);
            return;
        }

        await Points.ReplaceRoundAsync(ServiceId, _editGame, _editRoundKey.Value, awards);
    }

    private Task DeleteRound(GameRound round) => Points.DeleteRoundAsync(ServiceId, round.Key);

    /// <summary>
    /// A team's line in a round. Scored points, like the rest of this component - the
    /// screen numbers are the tally's job.
    /// </summary>
    private string RoundCell(GameRound round, int teamIndex)
    {
        int? place = round.PlaceFor(teamIndex);

        return place == null
            ? "—"
            : $"{GamePlacements.Ordinal(place.Value)}  {Format(round.PointsFor(teamIndex))}";
    }
}
