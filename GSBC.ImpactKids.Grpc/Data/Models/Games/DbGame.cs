namespace GSBC.ImpactKids.Grpc.Data.Models.Games;

/// <summary>
/// Settings for one game of the night. Only games that are named or have teams
/// combined get an entry - see <see cref="DbGameBoard.Games"/>.
/// </summary>
public class DbGame
{
    public required int Number { get; set; }

    public string? Name { get; set; }

    /// <summary>Alliance group per team, positionally indexed by <see cref="DbGameTeam.Index"/>.</summary>
    public List<int> Alliances { get; set; } = [];
}
