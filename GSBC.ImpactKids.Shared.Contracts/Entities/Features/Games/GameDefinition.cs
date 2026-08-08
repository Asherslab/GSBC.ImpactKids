using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

/// <summary>
/// Per game settings. A game only needs one of these if it is named or has teams
/// combined - an ordinary game is just its number.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record GameDefinition
{
    /// <summary>1 based, matching <see cref="GamePointRecord.GameNumber"/>.</summary>
    public required int Number { get; init; }

    /// <summary>Optional - games are numbered by default and naming one is opt in.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Alliance group per team, positionally indexed by <see cref="GameTeamDefinition.Index"/>.
    /// Teams sharing a value play this game as one side, and a tap on that side scores
    /// the full amount for each of them.
    /// <para>Empty - the usual case - means every team plays for itself.</para>
    /// </summary>
    public ImmutableList<int> Alliances { get; init; } = [];

    /// <summary>
    /// The group a team plays in. Teams outside the alliance list get a group of their
    /// own, numbered negatively so it can never collide with a real group id.
    /// </summary>
    public int GroupOf(int teamIndex) =>
        teamIndex >= 0 && teamIndex < Alliances.Count
            ? Alliances[teamIndex]
            : -1 - teamIndex;

    /// <summary>True once at least two teams share a group.</summary>
    public bool HasAlliances() =>
        Alliances.Count > 0 && Alliances.Distinct().Count() < Alliances.Count;

    public string DisplayName() => string.IsNullOrWhiteSpace(Name) ? $"Game {Number}" : Name;

    public static GameDefinition For(int number) => new() { Number = number };
}
