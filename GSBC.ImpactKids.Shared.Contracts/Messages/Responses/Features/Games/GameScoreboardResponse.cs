using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Games;

/// <summary>
/// Everything the unauthenticated wall display needs, and nothing else -
/// team colours and numbers, no people and no service detail beyond a title.
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

    /// <summary>Ordered by <see cref="TeamScoreLine.DisplayPoints"/>, highest first.</summary>
    public ImmutableList<TeamScoreLine> Teams { get; init; } = [];

    public static GameScoreboardResponse WithError(string error) => new()
    {
        Success = false,
        Error = error
    };
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TeamScoreLine
{
    public required GameTeam Team { get; init; }

    /// <summary>
    /// The number to put on screen, already resolved for the board's
    /// <see cref="GameScoreboardResponse.Mode"/>.
    /// </summary>
    public required int DisplayPoints { get; init; }

    /// <summary>Across every game, excluding behaviour points.</summary>
    public required int GamePoints { get; init; }

    public required int BehaviourPoints { get; init; }

    /// <summary>This game only.</summary>
    public required int CurrentGamePoints { get; init; }

    // Computed, so it must stay out of the contract - ImplicitFields.AllPublic would
    // otherwise try to assign it on deserialize and blow up building the serializer.
    [ProtoIgnore]
    public int Total => GamePoints + BehaviourPoints;
}
