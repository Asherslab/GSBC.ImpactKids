using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

/// <summary>
/// Placement scoring: a race is scored by finishing order rather than by tapping, so
/// first place is one tap worth ten points rather than ten taps.
/// <para>
/// The values are held per game (<see cref="GameDefinition.PlacementPoints"/>) and, like
/// everything else a leader touches, they are <b>scored</b> points - the displays do the
/// multiplying, so 10 here is what the wall shows as 10,000.
/// </para>
/// <para>
/// One round is one award: every side that ran gets a record, they all share a
/// <see cref="GamePointRecord.GroupId"/>, and each carries its own
/// <see cref="GamePointRecord.Place"/>. Order is stored rather than inferred from the
/// points, so a placing worth nothing is still a placing.
/// </para>
/// </summary>
public static class GamePlacements
{
    /// <summary>Nothing is worth less than nothing - a place out of the points scores 0.</summary>
    public const int MinPoints = 0;

    /// <summary>
    /// A placing is one award rather than a night's worth of tapping, so it is allowed to
    /// be worth more than <see cref="GameBoard.BonusPoints"/> ever is.
    /// </summary>
    public const int MaxPoints = 1_000;

    /// <summary>One placing per side, and the tile grid gives up long before this.</summary>
    public const int MaxPlaces = GameTeamDefaults.MaxTeams;

    /// <summary>What first place is worth when placement is switched on for a game.</summary>
    public const int DefaultTop = 10;

    /// <summary>
    /// The generators worth offering as a chip. Every night we have seen is one of these,
    /// which is what keeps the per place editor off the phone entirely.
    /// </summary>
    public static readonly IReadOnlyList<PlacementPreset> Presets =
    [
        new("Full slide", "10 · 9 · 8 · 7 …", DefaultTop, 1),
        new("Wide gaps", "10 · 7 · 4 · 1", DefaultTop, 3),
        new("Top three", "10 · 6 · 2, rest 0", DefaultTop, 4, Places: 3),
        new("Winner only", "10, rest 0", DefaultTop, 0, Places: 1)
    ];

    /// <summary>A named way of filling the placement list without typing a number.</summary>
    public record PlacementPreset(string Name, string Summary, int Top, int Step, int? Places = null)
    {
        /// <summary>The list this preset makes for a game with <paramref name="sides"/> sides.</summary>
        public ImmutableList<int> Build(int sides) =>
            Generate(Top, Step, Math.Min(Places ?? sides, sides));

        /// <summary>Whether a game is already running on exactly what this preset would make.</summary>
        public bool Matches(ImmutableList<int>? points, int sides) =>
            points != null && points.SequenceEqual(Build(sides));
    }

    /// <summary>
    /// Points for the first <paramref name="places"/> places, dropping by
    /// <paramref name="step"/> and never going below zero. Places past the list score
    /// nothing, so a short list is how "only the top three score" is said.
    /// </summary>
    public static ImmutableList<int> Generate(int top, int step, int places)
    {
        places = Math.Clamp(places, 1, MaxPlaces);

        int value = Clamp(top);

        List<int> points = [];

        for (int index = 0; index < places; index++)
        {
            points.Add(value);

            value = Clamp(value - step);
        }

        return [..points];
    }

    /// <summary>
    /// The list a game should be given when placement is switched on, sized to the sides
    /// playing it. Chosen rather than asked for - switching on is one tap, and the values
    /// are editable afterwards on the totals page.
    /// </summary>
    public static ImmutableList<int> Default(int sides) => Presets[0].Build(sides);

    /// <summary>
    /// Trims a stored list to something usable: values in range, no more places than
    /// there can be, and null for a list with nothing in it - null is what says a game is
    /// scored by tapping rather than by placement, so an empty list must not survive as
    /// a game that cannot be scored.
    /// </summary>
    public static ImmutableList<int>? Normalise(IEnumerable<int>? points)
    {
        if (points == null)
            return null;

        ImmutableList<int> trimmed = [..points.Take(MaxPlaces).Select(Clamp)];

        return trimmed.Count > 0 ? trimmed : null;
    }

    /// <summary>
    /// What a place is worth. <paramref name="place"/> is 1 based, and anything past the
    /// end of the list scores nothing rather than throwing - the list is allowed to be
    /// shorter than the field.
    /// </summary>
    public static int PointsAt(ImmutableList<int>? points, int place) =>
        points != null && place >= 1 && place <= points.Count ? points[place - 1] : 0;

    /// <summary>
    /// Competition ranking - the place a side gets when the sides ahead of it have
    /// already taken <paramref name="sidesAhead"/> spots between them. Two sides tied for
    /// first are both 1st and the next one is 3rd, which is the rule the board and the
    /// reveal already use.
    /// </summary>
    public static int PlaceAfter(int sidesAhead) => sidesAhead + 1;

    /// <summary>
    /// "1st", "7th", "11th". The teens are the trap - "11st" on a wall would be the only
    /// thing anyone remembered. The reveal words placings the same way and calls this.
    /// </summary>
    public static string Ordinal(int place)
    {
        bool teen = place % 100 is >= 11 and <= 13;

        string suffix = teen
            ? "th"
            : (place % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };

        return $"{place}{suffix}";
    }

    private static int Clamp(int points) => Math.Clamp(points, MinPoints, MaxPoints);
}
