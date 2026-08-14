using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

/// <summary>
/// Shared scoreboard settings for one service - which game is running, who the teams
/// are, how the buttons are configured, and what the public display is allowed to show.
/// One per service.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record GameBoard : IIdentifiable
{
    public required Guid Id        { get; init; }
    public required Guid ServiceId { get; init; }

    /// <summary>1 based. Bumped by "new game", which starts a fresh split.</summary>
    public required int CurrentGame { get; init; }

    /// <summary>
    /// The teams playing tonight, ordered by <see cref="GameTeamDefinition.Index"/>.
    /// Empty falls back to the usual four - see <see cref="EffectiveTeams"/>.
    /// </summary>
    public ImmutableList<GameTeamDefinition> Teams { get; init; } = [];

    /// <summary>
    /// Only the games that need settings of their own - a name, or teams combined.
    /// Sparse on purpose: an ordinary game is just its number.
    /// </summary>
    public ImmutableList<GameDefinition> Games { get; init; } = [];

    /// <summary>Points awarded by the main tap and removed by the minus button.</summary>
    public required int StepPoints { get; init; }

    /// <summary>Points awarded by the secondary bonus button.</summary>
    public required int BonusPoints { get; init; }

    /// <summary>
    /// What one scored point is worth on the wall displays. Scoring itself is untouched -
    /// a tap is still one point - this only scales what the screens show.
    /// <para>
    /// It is the night's multiplier: the value the first game runs at and what every game
    /// without one of its own inherits. A single game can override it; see
    /// <see cref="GameDefinition.Multiplier"/>.
    /// </para>
    /// </summary>
    public required int PointsMultiplier { get; init; }

    /// <summary>
    /// What one behaviour point is worth on the displays. Its own value rather than the
    /// night's: behaviour points are handed out all evening for something other than
    /// winning a game, so what they are worth on screen is a separate decision.
    /// </summary>
    public required int BehaviourPointsMultiplier { get; init; }

    /// <summary>Whether the wall display shows the night's totals or just this game.</summary>
    public required GameDisplayMode DisplayMode { get; init; }

    /// <summary>Public display shows a holding screen instead of the scores.</summary>
    public required bool Hidden { get; init; }

    /// <summary>
    /// Public display is frozen. Scoring carries on as normal - the display just
    /// ignores anything awarded after <see cref="PausedAt"/> until it resumes.
    /// </summary>
    public required bool Paused { get; init; }

    public required DateTime? PausedAt { get; init; }

    /// <summary>
    /// Which step of the end of night reveal the public display is showing, or null when
    /// no reveal is running.
    /// <para>
    /// It lives on the board because the reveal is driven from a leader's phone and plays
    /// on a screen that cannot be touched - the board is the only thing both ends share.
    /// </para>
    /// </summary>
    public int? RevealStep { get; init; }

    /// <summary>
    /// Last write wins, so a board edit queued offline can never clobber a newer one.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    /// <summary>Teams to score against, defaulted for a board that has never been edited.</summary>
    public ImmutableList<GameTeamDefinition> EffectiveTeams() =>
        Teams.Count > 0 ? Teams : GameTeamDefaults.Default();

    /// <summary>Settings for a game, or a plain unnamed game with no alliances.</summary>
    public GameDefinition GameAt(int number) =>
        Games.FirstOrDefault(x => x.Number == number) ?? GameDefinition.For(number);

    public GameDefinition CurrentGameDefinition() => GameAt(CurrentGame);

    /// <summary>
    /// The games that are part of the night, in order, out of the <paramref name="gamesPlayed"/>
    /// the service has reached. Planned and hidden games are left out.
    /// <para>
    /// Everything that renders a game must go through this - the tally's columns, the
    /// names and points sent to the wall, and the reveal's running order. The reveal
    /// counts its steps from the length of those lists on both ends, so one end filtering
    /// where the other does not slides every later step out of place.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> CountingGames(int gamesPlayed) =>
    [
        ..Enumerable
            .Range(1, Math.Max(gamesPlayed, 0))
            .Where(number => GameAt(number).CountsTowardNight())
    ];

    /// <summary>
    /// The highest game the board knows about at all, planned ones included - what the set
    /// up list runs to. A game planned for later exists here long before it is played.
    /// </summary>
    public int HighestDefinedGame() =>
        Games.Count == 0 ? 0 : Games.Max(x => x.Number);

    /// <summary>The next game number a planned game would be added at.</summary>
    public int NextGameNumber(int gamesPlayed) =>
        Math.Max(gamesPlayed, HighestDefinedGame()) + 1;

    /// <summary>
    /// The next game there is to move on to - one already played, or one set up ahead and
    /// waiting - or null when this is the end of the night so far.
    /// <para>
    /// It is what decides whether the scoring page offers to step forward or to start a
    /// new game: with a night planned out in advance, offering to create game 7 while game
    /// 7 is sitting there named and waiting is how you end up with two of them.
    /// </para>
    /// <para>
    /// Hidden games are stepped over. A voided game is not part of the night, and walking
    /// into one from the arrow would score points that quietly do not count.
    /// </para>
    /// </summary>
    public int? NextGameAfter(int current, int gamesPlayed)
    {
        int last = Math.Max(gamesPlayed, HighestDefinedGame());

        for (int number = current + 1; number <= last; number++)
        {
            bool exists = number <= gamesPlayed || Games.Any(x => x.Number == number);

            if (exists && !GameAt(number).Hidden)
                return number;
        }

        return null;
    }

    /// <summary>
    /// The planned game to play next, or null if none are waiting. "New game" picks this
    /// up instead of opening a blank one, which is what makes planning the night ahead
    /// worth doing.
    /// </summary>
    public int? NextPlannedGame(int after) =>
        Games
            .Where(x => x.Planned && x.Number > after)
            .OrderBy(x => x.Number)
            .Select(x => (int?)x.Number)
            .FirstOrDefault();

    /// <summary>
    /// What one point in a game is worth on screen - the game's own multiplier, or the
    /// last game before it that set one, or the board's.
    /// </summary>
    public int MultiplierFor(int gameNumber) =>
        GameMultipliers.For(gameNumber, PointsMultiplier, OwnMultiplier);

    /// <summary>Multiplier per game, index 0 being game 1.</summary>
    public int[] MultipliersThrough(int gamesPlayed) =>
        GameMultipliers.PerGame(gamesPlayed, PointsMultiplier, OwnMultiplier);

    /// <summary>
    /// Behaviour points belong to no game, so they never follow a game's multiplier -
    /// they have one of their own.
    /// </summary>
    public int BehaviourMultiplier() => GameMultipliers.Normalise(BehaviourPointsMultiplier);

    private int? OwnMultiplier(int gameNumber) =>
        Games.FirstOrDefault(x => x.Number == gameNumber)?.Multiplier;

    /// <summary>Replaces the settings for one game, dropping the entry when it is plain again.</summary>
    public GameBoard WithGame(GameDefinition game)
    {
        ImmutableList<GameDefinition> rest = Games.RemoveAll(x => x.Number == game.Number);

        // A planned game is often nothing but a slot in the running order, and a hidden one
        // may hold nothing but the decision to void it - so both count as settings.
        bool worthKeeping = game.HasSettings();

        return this with
        {
            Games = worthKeeping
                ? rest.Add(game).Sort((a, b) => a.Number.CompareTo(b.Number))
                : rest
        };
    }

    public static GameBoard Default(Guid serviceId) => new()
    {
        Id = Guid.Empty,
        ServiceId = serviceId,
        CurrentGame = 1,
        Teams = GameTeamDefaults.Default(),
        Games = [],
        StepPoints = 1,
        BonusPoints = 5,
        PointsMultiplier = GameMultipliers.Default,
        BehaviourPointsMultiplier = GameMultipliers.Default,
        DisplayMode = GameDisplayMode.Totals,
        Hidden = false,
        Paused = false,
        PausedAt = null,
        RevealStep = null,
        UpdatedAt = DateTime.UnixEpoch
    };
}
