using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

/// <summary>
/// Per game settings. A game only needs one of these if it is named, has teams combined
/// or runs at its own multiplier - an ordinary game is just its number.
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
    /// What one scored point is worth on the displays for this game, or null to follow
    /// the game before it. See <see cref="GameMultipliers"/>.
    /// </summary>
    public int? Multiplier { get; init; }

    /// <summary>
    /// What each finishing place is worth in scored points, index 0 being first, or null
    /// for an ordinary game scored by tapping.
    /// <para>
    /// Its presence <b>is</b> the mode - there is no separate flag to keep in step. It
    /// deliberately does not inherit from the game before it the way
    /// <see cref="Multiplier"/> does: a multiplier is a rate, but placement is a way of
    /// playing, and game 4 quietly becoming a race because game 3 was one is exactly the
    /// confusion this feature exists to remove.
    /// </para>
    /// </summary>
    public ImmutableList<int>? PlacementPoints { get; init; }

    /// <summary>Scored by finishing order rather than by tapping.</summary>
    public bool IsPlacement() => PlacementPoints is { Count: > 0 };

    /// <summary>
    /// Set up ahead of the night and not part of it yet. A big night is planned out in the
    /// hall - names, multipliers, which games are races - before anybody goes near the
    /// field, and eight planned games must not put eight empty columns on the wall.
    /// <para>
    /// Clears itself the first time the game is scored or opened on the scoring page, so
    /// nobody has to remember to "start" it.
    /// </para>
    /// </summary>
    public bool Planned { get; init; }

    /// <summary>
    /// Voided. A game that went wrong: gone from the tally, the wall and the reveal, and
    /// its points stop counting toward the night.
    /// <para>
    /// Points are left alone rather than deleted, so un-hiding puts the game back exactly
    /// as it was. Unlike <see cref="Planned"/> this never clears itself - it was somebody's
    /// decision.
    /// </para>
    /// </summary>
    public bool Hidden { get; init; }

    /// <summary>
    /// Whether this game is part of the night: it shows on the tally and the wall, gets a
    /// round in the reveal, and its points count. The one rule, so the wall and the phone
    /// cannot disagree about how many rounds the reveal has.
    /// </summary>
    public bool CountsTowardNight() => !Planned && !Hidden;

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
