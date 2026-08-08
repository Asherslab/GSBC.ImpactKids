using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

/// <summary>
/// Default team names and colours, shared by the scoring tool, the wall display and the
/// server so a board created on one device looks the same everywhere.
/// <para>
/// Colours past the named palette are derived from the team index rather than actually
/// random, so two devices that add a fifth team while offline still agree on its colour.
/// </para>
/// </summary>
public static partial class GameTeamDefaults
{
    public const int DefaultTeamCount = 4;
    public const int MinTeams         = 2;

    /// <summary>Well past anything sane, but a group-of-four split of a big night fits.</summary>
    public const int MaxTeams = 40;

    public const int MaxNameLength     = 20;
    public const int MaxGameNameLength = 32;

    /// <summary>The colours the room already knows. The first four are the usual teams.</summary>
    private static readonly (string Name, string Colour)[] Palette =
    [
        ("Red", "#d32f2f"),
        ("Green", "#2e7d32"),
        ("Blue", "#1565c0"),
        ("Yellow", "#e5a100"),
        ("Purple", "#7b1fa2"),
        ("Orange", "#ef6c00"),
        ("Teal", "#00838f"),
        ("Pink", "#c2185b"),
        ("Lime", "#689f38"),
        ("Indigo", "#3949ab"),
        ("Brown", "#6d4c41"),
        ("Cyan", "#0097a7")
    ];

    /// <summary>The team that belongs at a position when nobody has customised it.</summary>
    public static GameTeamDefinition TeamAt(int index) => new()
    {
        Index = index,
        Name = DefaultName(index),
        Colour = DefaultColour(index)
    };

    public static ImmutableList<GameTeamDefinition> Create(int count) =>
        Enumerable.Range(0, Math.Clamp(count, MinTeams, MaxTeams))
            .Select(TeamAt)
            .ToImmutableList();

    public static ImmutableList<GameTeamDefinition> Default() => Create(DefaultTeamCount);

    public static string DefaultName(int index) =>
        index >= 0 && index < Palette.Length ? Palette[index].Name : $"Team {index + 1}";

    public static string DefaultColour(int index)
    {
        if (index >= 0 && index < Palette.Length)
            return Palette[index].Colour;

        // Golden angle around the wheel, so consecutive teams never land on
        // neighbouring hues no matter how many there are.
        double hue = index * 137.508 % 360;

        return FromHsl(hue, saturation: .62, lightness: .40);
    }

    /// <summary>A colour the operator asked for by tapping "shuffle" - any hue, same depth.</summary>
    public static string RandomColour(Random random) =>
        FromHsl(random.Next(360), saturation: .62, lightness: .40);

    public static bool IsValidColour(string? colour) =>
        colour != null && ColourPattern().IsMatch(colour);

    public static string FromHsl(double hue, double saturation, double lightness)
    {
        double c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        double x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        double m = lightness - c / 2;

        (double r, double g, double b) = hue switch
        {
            < 60  => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _     => (c, 0d, x)
        };

        return "#" + Component(r + m) + Component(g + m) + Component(b + m);
    }

    private static string Component(double value) =>
        ((int)Math.Round(Math.Clamp(value, 0, 1) * 255))
        .ToString("x2", CultureInfo.InvariantCulture);

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColourPattern();
}
