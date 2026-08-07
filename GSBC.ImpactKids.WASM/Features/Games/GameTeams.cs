using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

namespace GSBC.ImpactKids.WASM.Features.Games;

/// <summary>
/// Team presentation, shared by the scoring tool, the tally page and the wall display
/// so a colour never means two different things.
/// </summary>
public static class GameTeams
{
    /// <summary>Two team games use the first two.</summary>
    public static readonly GameTeam[] All =
    [
        GameTeam.Red,
        GameTeam.Green,
        GameTeam.Blue,
        GameTeam.Yellow
    ];

    public static IEnumerable<GameTeam> Take(int teamCount) => All.Take(teamCount is 2 or 4 ? teamCount : 4);

    public static string Label(GameTeam team) => team switch
    {
        GameTeam.Red    => "Red",
        GameTeam.Green  => "Green",
        GameTeam.Blue   => "Blue",
        GameTeam.Yellow => "Yellow",
        _               => team.ToString()
    };

    public static string Colour(GameTeam team) => team switch
    {
        GameTeam.Red    => "#d32f2f",
        GameTeam.Green  => "#2e7d32",
        GameTeam.Blue   => "#1565c0",
        GameTeam.Yellow => "#e5a100",
        _               => "#555555"
    };
}
