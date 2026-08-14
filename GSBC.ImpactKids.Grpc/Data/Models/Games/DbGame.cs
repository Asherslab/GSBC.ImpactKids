namespace GSBC.ImpactKids.Grpc.Data.Models.Games;

/// <summary>
/// Settings for one game of the night. Only games that are named, have teams combined or
/// run at their own multiplier get an entry - see <see cref="DbGameBoard.Games"/>.
/// </summary>
public class DbGame
{
    public required int Number { get; set; }

    public string? Name { get; set; }

    /// <summary>Alliance group per team, positionally indexed by <see cref="DbGameTeam.Index"/>.</summary>
    public List<int> Alliances { get; set; } = [];

    /// <summary>
    /// Display only scaling for this game, or null to follow the game before it. See
    /// <see cref="Shared.Contracts.Entities.Features.Games.GameMultipliers"/>.
    /// </summary>
    public int? Multiplier { get; set; }

    /// <summary>
    /// Scored points per finishing place, index 0 being first, or null for a game scored
    /// by tapping. See
    /// <see cref="Shared.Contracts.Entities.Features.Games.GamePlacements"/>.
    /// </summary>
    public List<int>? PlacementPoints { get; set; }

    /// <summary>Set up ahead of the night and not part of it yet.</summary>
    public bool Planned { get; set; }

    /// <summary>Voided - out of the tally, the wall and the reveal, and its points do not count.</summary>
    public bool Hidden { get; set; }
}
