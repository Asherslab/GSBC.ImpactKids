namespace GSBC.ImpactKids.Grpc.Data.Models.Games;

/// <summary>
/// One team on a board. Stored as JSON inside <see cref="DbGameBoard"/> - the whole
/// list is rewritten together on every board edit, so there is nothing to relate to.
/// </summary>
public class DbGameTeam
{
    public required int    Index  { get; set; }
    public required string Name   { get; set; }
    public required string Colour { get; set; }
}
