namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

/// <summary>
/// One team on a service's board.
/// <para>
/// <see cref="Index"/> is what every point record points at, so it has to stay stable
/// for the life of the service. Teams are only ever appended or removed from the end,
/// which keeps the indexes contiguous and stops a rename from moving anyone's score.
/// </para>
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record GameTeamDefinition
{
    public required int Index { get; init; }

    public required string Name { get; init; }

    /// <summary>#rrggbb. Dark enough for white text - the tiles are colour filled.</summary>
    public required string Colour { get; init; }
}
