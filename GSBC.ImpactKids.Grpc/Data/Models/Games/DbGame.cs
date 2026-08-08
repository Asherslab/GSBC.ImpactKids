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
}
