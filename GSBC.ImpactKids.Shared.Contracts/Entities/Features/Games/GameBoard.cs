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

    /// <summary>Replaces the settings for one game, dropping the entry when it is plain again.</summary>
    public GameBoard WithGame(GameDefinition game)
    {
        ImmutableList<GameDefinition> rest = Games.RemoveAll(x => x.Number == game.Number);

        bool worthKeeping = !string.IsNullOrWhiteSpace(game.Name) || game.Alliances.Count > 0;

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
        DisplayMode = GameDisplayMode.Totals,
        Hidden = false,
        Paused = false,
        PausedAt = null,
        UpdatedAt = DateTime.UnixEpoch
    };
}
