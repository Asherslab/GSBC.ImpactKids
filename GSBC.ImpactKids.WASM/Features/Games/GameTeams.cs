using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

namespace GSBC.ImpactKids.WASM.Features.Games;

/// <summary>
/// Team presentation helpers shared by the scoring tool, the tally page and the wall
/// display, so a colour never means two different things.
/// </summary>
public static class GameTeams
{
    private static readonly Random Random = new();

    /// <summary>Grows or shrinks a team list, filling new slots with the next default team.</summary>
    public static ImmutableList<GameTeamDefinition> Resize(
        ImmutableList<GameTeamDefinition> teams,
        int                               count
    )
    {
        count = Math.Clamp(count, GameTeamDefaults.MinTeams, GameTeamDefaults.MaxTeams);

        if (count <= teams.Count)
            return teams.GetRange(0, count);

        return teams.AddRange(
            Enumerable.Range(teams.Count, count - teams.Count).Select(GameTeamDefaults.TeamAt)
        );
    }

    public static ImmutableList<GameTeamDefinition> Rename(
        ImmutableList<GameTeamDefinition> teams,
        int                               index,
        string?                           name
    ) => Replace(teams, index, team => team with
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? GameTeamDefaults.DefaultName(index)
                : name.Trim()[..Math.Min(name.Trim().Length, GameTeamDefaults.MaxNameLength)]
        }
    );

    /// <summary>Rolls a fresh colour for one team - the "I don't like that one" button.</summary>
    public static ImmutableList<GameTeamDefinition> ShuffleColour(
        ImmutableList<GameTeamDefinition> teams,
        int                               index
    ) => Replace(teams, index, team => team with { Colour = GameTeamDefaults.RandomColour(Random) });

    /// <summary>Sets an exact colour, for when shuffling is not going to land on it.</summary>
    public static ImmutableList<GameTeamDefinition> SetColour(
        ImmutableList<GameTeamDefinition> teams,
        int                               index,
        string?                           colour
    ) => Replace(teams, index, team => team with
        {
            Colour = GameTeamDefaults.IsValidColour(colour) ? colour! : team.Colour
        }
    );

    private static ImmutableList<GameTeamDefinition> Replace(
        ImmutableList<GameTeamDefinition>                 teams,
        int                                               index,
        Func<GameTeamDefinition, GameTeamDefinition> mutate
    )
    {
        int position = teams.FindIndex(x => x.Index == index);

        return position < 0 ? teams : teams.SetItem(position, mutate(teams[position]));
    }

    /// <summary>A left to right gradient, so a combined tile shows both teams' colours.</summary>
    public static string Background(IReadOnlyList<GameTeamDefinition> teams) =>
        teams.Count switch
        {
            0 => "#555555",
            1 => teams[0].Colour,
            _ => $"linear-gradient(135deg, {string.Join(", ", teams.Select(x => x.Colour))})"
        };
}
