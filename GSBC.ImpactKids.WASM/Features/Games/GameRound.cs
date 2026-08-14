using System.Collections.Immutable;

namespace GSBC.ImpactKids.WASM.Features.Games;

/// <summary>
/// One heat of a game as it was scored - who ran, where they came and what they got.
/// <para>
/// A round is not stored as a thing of its own. It is the records of one award read back
/// together: they share a <c>GroupId</c>, which is also what undo has always worked on.
/// Rounds all carry the same game number, so the tally and the end of night reveal go on
/// seeing one game with one total however many heats it took.
/// </para>
/// </summary>
public sealed record GameRound
{
    /// <summary>The award's group id - what identifies this round for an edit or a delete.</summary>
    public required Guid Key { get; init; }

    public required int GameNumber { get; init; }

    /// <summary>When the round was awarded, which is also what orders the rounds.</summary>
    public required DateTime Awarded { get; init; }

    /// <summary>Placed teams, best first.</summary>
    public required ImmutableList<GameRoundEntry> Entries { get; init; }

    /// <summary>Round 1, 2, 3 within its game - a label, worked out from the ordering.</summary>
    public required int Number { get; init; }

    public int PointsFor(int teamIndex) =>
        Entries.Where(x => x.TeamIndex == teamIndex).Sum(x => x.Points);

    public int? PlaceFor(int teamIndex) =>
        Entries.FirstOrDefault(x => x.TeamIndex == teamIndex)?.Place;

    /// <summary>The finishing order in the shape the editors work in - tied teams grouped.</summary>
    public ImmutableList<ImmutableList<int>> Order() =>
        GamePlacementOrder.FromPlaces(Entries.Select(x => (x.TeamIndex, x.Place)));
}

/// <summary>One team's line in a round.</summary>
public sealed record GameRoundEntry
{
    public required Guid RecordId { get; init; }

    public required int TeamIndex { get; init; }

    /// <summary>1 based, competition ranked - tied teams share a place.</summary>
    public required int Place { get; init; }

    /// <summary>Scored points, which can be zero: a place out of the points is still a place.</summary>
    public required int Points { get; init; }
}

/// <summary>What one place in a round is worth to the team or teams that took it.</summary>
public sealed record GamePlacementAward
{
    /// <summary>The teams on this place - more than one is a tie, or a combined side.</summary>
    public required IReadOnlyList<int> TeamIndexes { get; init; }

    public required int Place { get; init; }

    public required int Points { get; init; }
}
