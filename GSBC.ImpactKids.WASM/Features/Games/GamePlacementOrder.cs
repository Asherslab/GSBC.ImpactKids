using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

namespace GSBC.ImpactKids.WASM.Features.Games;

/// <summary>
/// A finishing order being built or edited: a list of groups, best first, where a group
/// holding more than one entrant is a tie. The entrant is an int - a side on the scoring
/// page, a team on the totals page - so both ends share one set of rules.
/// <para>
/// Places are worked out from the shape rather than stored per row, which is what keeps
/// competition ranking honest while the order is being changed: two tied for first are
/// both 1st and the next is 3rd, with nothing to fall out of step.
/// </para>
/// </summary>
public static class GamePlacementOrder
{
    public static readonly ImmutableList<ImmutableList<int>> Empty = [];

    /// <summary>The place a group finishes in, counting everyone ahead of it.</summary>
    public static int PlaceOf(ImmutableList<ImmutableList<int>> groups, int groupIndex)
    {
        int ahead = 0;

        for (int index = 0; index < groupIndex && index < groups.Count; index++)
            ahead += groups[index].Count;

        return GamePlacements.PlaceAfter(ahead);
    }

    /// <summary>Which group an entrant is in, or -1 when it has not been placed.</summary>
    public static int GroupOf(ImmutableList<ImmutableList<int>> groups, int entrant) =>
        groups.FindIndex(group => group.Contains(entrant));

    /// <summary>The place an entrant finished in, or null when it has not been placed.</summary>
    public static int? PlaceFor(ImmutableList<ImmutableList<int>> groups, int entrant)
    {
        int group = GroupOf(groups, entrant);

        return group < 0 ? null : PlaceOf(groups, group);
    }

    /// <summary>The place the next entrant to be placed would take.</summary>
    public static int NextPlace(ImmutableList<ImmutableList<int>> groups) =>
        GamePlacements.PlaceAfter(groups.Sum(group => group.Count));

    /// <summary>The place a tie would join - the last one placed, or null if nothing is.</summary>
    public static int? TiePlace(ImmutableList<ImmutableList<int>> groups) =>
        groups.Count == 0 ? null : PlaceOf(groups, groups.Count - 1);

    /// <summary>Places an unplaced entrant last, or takes a placed one back out.</summary>
    public static ImmutableList<ImmutableList<int>> Toggle(
        ImmutableList<ImmutableList<int>> groups,
        int                               entrant
    )
    {
        int group = GroupOf(groups, entrant);

        return group < 0
            ? groups.Add([entrant])
            : Remove(groups, entrant);
    }

    /// <summary>
    /// Puts an entrant in with the last group placed, which is the dead heat the leader
    /// just watched. Nothing placed yet means there is nothing to tie with.
    /// </summary>
    public static ImmutableList<ImmutableList<int>> Tie(
        ImmutableList<ImmutableList<int>> groups,
        int                               entrant
    )
    {
        ImmutableList<ImmutableList<int>> without = Remove(groups, entrant);

        return without.Count == 0
            ? without.Add([entrant])
            : without.SetItem(without.Count - 1, without[^1].Add(entrant));
    }

    /// <summary>Moves an entrant to a place of its own, pushing everything from there down.</summary>
    public static ImmutableList<ImmutableList<int>> MoveToPlace(
        ImmutableList<ImmutableList<int>> groups,
        int                               entrant,
        int                               place
    )
    {
        ImmutableList<ImmutableList<int>> without = Remove(groups, entrant);

        int index = IndexOfPlace(without, place);

        return without.Insert(Math.Clamp(index, 0, without.Count), [entrant]);
    }

    /// <summary>Moves an entrant into the group that already holds a place, making it a tie.</summary>
    public static ImmutableList<ImmutableList<int>> TieWithPlace(
        ImmutableList<ImmutableList<int>> groups,
        int                               entrant,
        int                               place
    )
    {
        ImmutableList<ImmutableList<int>> without = Remove(groups, entrant);

        int index = IndexOfPlace(without, place);

        return index >= 0 && index < without.Count
            ? without.SetItem(index, without[index].Add(entrant))
            : MoveToPlace(without, entrant, place);
    }

    /// <summary>Takes an entrant out of the order, dropping a group left empty.</summary>
    public static ImmutableList<ImmutableList<int>> Remove(
        ImmutableList<ImmutableList<int>> groups,
        int                               entrant
    ) =>
    [
        ..groups
            .Select(group => group.Remove(entrant))
            .Where(group => group.Count > 0)
    ];

    /// <summary>
    /// Rebuilds the order from records that were already awarded, so an edit starts from
    /// what actually happened. Entrants on the same stored place are one tie.
    /// </summary>
    public static ImmutableList<ImmutableList<int>> FromPlaces(IEnumerable<(int Entrant, int Place)> placed) =>
    [
        ..placed
            .GroupBy(x => x.Place)
            .OrderBy(group => group.Key)
            .Select(ImmutableList<int> (group) => [..group.Select(x => x.Entrant).Distinct()])
    ];

    /// <summary>
    /// The group index a place sits at, or the end of the list when nothing is that far
    /// down - inserting past the last group is how a new last place is made.
    /// </summary>
    private static int IndexOfPlace(ImmutableList<ImmutableList<int>> groups, int place)
    {
        for (int index = 0; index < groups.Count; index++)
        {
            if (PlaceOf(groups, index) >= place)
                return index;
        }

        return groups.Count;
    }
}
