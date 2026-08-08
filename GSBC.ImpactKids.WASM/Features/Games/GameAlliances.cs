using System.Collections.Immutable;

namespace GSBC.ImpactKids.WASM.Features.Games;

/// <summary>
/// Editing helpers for a game's alliance list - the flat "group id per team" array that
/// says which teams are playing this one game as a single side.
/// <para>
/// Group ids are always renumbered from zero in team order, so two devices that build the
/// same grouping end up with the same array and last write wins stays predictable.
/// </para>
/// </summary>
public static class GameAlliances
{
    /// <summary>No teams combined - stored as an empty list rather than 0,1,2,3.</summary>
    public static ImmutableList<int> None => [];

    /// <summary>Everyone in their own group, ready to be edited.</summary>
    private static int[] Split(ImmutableList<int> alliances, int teamCount)
    {
        int[] groups = new int[teamCount];

        for (int team = 0; team < teamCount; team++)
        {
            groups[team] = team < alliances.Count ? alliances[team] : teamCount + team;
        }

        return groups;
    }

    /// <summary>The teams in each side, in team order, sides ordered by their first team.</summary>
    public static IReadOnlyList<ImmutableList<int>> Groups(ImmutableList<int> alliances, int teamCount)
    {
        int[] groups = Split(alliances, teamCount);

        return Enumerable.Range(0, teamCount)
            .GroupBy(team => groups[team])
            .OrderBy(group => group.Min())
            .Select(group => group.ToImmutableList())
            .ToList();
    }

    /// <summary>Only the sides with more than one team in them.</summary>
    public static IReadOnlyList<ImmutableList<int>> Combined(ImmutableList<int> alliances, int teamCount) =>
        Groups(alliances, teamCount).Where(x => x.Count > 1).ToList();

    /// <summary>Puts the given teams on one side, pulling them out of any side they were in.</summary>
    public static ImmutableList<int> Combine(
        ImmutableList<int> alliances,
        int                teamCount,
        IEnumerable<int>   teams
    )
    {
        int[]      groups   = Split(alliances, teamCount);
        List<int>  selected = teams.Where(x => x >= 0 && x < teamCount).Distinct().ToList();

        if (selected.Count < 2)
            return Normalise(groups, teamCount);

        int target = selected.Min();

        foreach (int team in selected)
        {
            groups[team] = groups[target];
        }

        return Normalise(groups, teamCount);
    }

    /// <summary>Breaks a side apart, putting each of its teams back on its own.</summary>
    public static ImmutableList<int> Separate(
        ImmutableList<int> alliances,
        int                teamCount,
        IEnumerable<int>   teams
    )
    {
        int[] groups = Split(alliances, teamCount);
        int   next   = teamCount * 2;

        foreach (int team in teams.Where(x => x >= 0 && x < teamCount))
        {
            groups[team] = next++;
        }

        return Normalise(groups, teamCount);
    }

    /// <summary>Combines teams two at a time in board order - the usual "four into two".</summary>
    public static ImmutableList<int> PairUp(int teamCount) =>
        teamCount < 4
            ? None
            : Enumerable.Range(0, teamCount).Select(team => team / 2).ToImmutableList();

    /// <summary>Renumbers from zero, and collapses "everyone alone" back to empty.</summary>
    private static ImmutableList<int> Normalise(int[] groups, int teamCount)
    {
        Dictionary<int, int> renumbered = [];
        int[]                result     = new int[teamCount];

        for (int team = 0; team < teamCount; team++)
        {
            if (!renumbered.TryGetValue(groups[team], out int id))
            {
                id = renumbered.Count;
                renumbered[groups[team]] = id;
            }

            result[team] = id;
        }

        return renumbered.Count == teamCount ? None : [..result];
    }
}
