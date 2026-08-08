using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Games;

/// <summary>
/// Everything the unauthenticated wall display needs, and nothing else -
/// team colours and numbers, no people and no service detail beyond a title.
/// <para>
/// Every point value in here is already multiplied for the screen - see
/// <see cref="Entities.Features.Games.GameMultipliers"/>. The displays render what they
/// are given, so the scaling is applied once, server side, where the per game multipliers
/// live. Raw scored points never reach these screens.
/// </para>
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class GameScoreboardResponse
{
    public required bool    Success { get; init; }
    public          string? Error   { get; init; }

    public string? Title { get; init; }

    /// <summary>The operator has hidden the scores - show a holding screen.</summary>
    public bool Hidden { get; init; }

    /// <summary>Scores are frozen at a point in time and will not move.</summary>
    public bool Paused { get; init; }

    public GameDisplayMode Mode { get; init; }

    public int CurrentGame { get; init; }

    /// <summary>What to call the current game - its name if it has one, else "Game 3".</summary>
    public string? CurrentGameName { get; init; }

    /// <summary>At least two teams are playing the current game as one side.</summary>
    public bool CurrentGameHasAlliances { get; init; }

    /// <summary>Ordered by <see cref="TeamScoreLine.DisplayPoints"/>, highest first.</summary>
    public ImmutableList<TeamScoreLine> Teams { get; init; } = [];

    /// <summary>
    /// Games with a score in them, or the current game, whichever is further along. It
    /// is the length of every <see cref="TeamScoreLine.PerGamePoints"/> list.
    /// </summary>
    public int GamesPlayed { get; init; }

    /// <summary>What to call each game, index 0 being game 1. Always <see cref="GamesPlayed"/> long.</summary>
    public ImmutableList<string> GameNames { get; init; } = [];

    /// <summary>
    /// Step of the end of night reveal the display should be showing, or null when no
    /// reveal is running. Driven from a phone; see <see cref="Entities.Features.Games.GameBoard.RevealStep"/>.
    /// </summary>
    public int? RevealStep { get; init; }

    public static GameScoreboardResponse WithError(string error) => new()
    {
        Success = false,
        Error = error
    };
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TeamScoreLine
{
    public required int    TeamIndex { get; init; }
    public required string Name      { get; init; }
    public required string Colour    { get; init; }

    /// <summary>
    /// Teams sharing this value are playing the current game combined. Distinct per
    /// team when nothing is combined, so the display can group on it unconditionally.
    /// </summary>
    public required int AllianceGroup { get; init; }

    /// <summary>
    /// The number to put on screen, already resolved for the board's
    /// <see cref="GameScoreboardResponse.Mode"/> and already multiplied.
    /// </summary>
    public required int DisplayPoints { get; init; }

    /// <summary>Across every game, excluding behaviour points.</summary>
    public required int GamePoints { get; init; }

    public required int BehaviourPoints { get; init; }

    /// <summary>This game only.</summary>
    public required int CurrentGamePoints { get; init; }

    /// <summary>
    /// Points per game, index 0 being game 1, as long as
    /// <see cref="GameScoreboardResponse.GamesPlayed"/>. Only the reveal needs the split;
    /// the ordinary board renders <see cref="DisplayPoints"/>.
    /// </summary>
    public ImmutableList<int> PerGamePoints { get; init; } = [];

    // Computed, so it must stay out of the contract - ImplicitFields.AllPublic would
    // otherwise try to assign it on deserialize and blow up building the serializer.
    [ProtoIgnore]
    public int Total => GamePoints + BehaviourPoints;
}
