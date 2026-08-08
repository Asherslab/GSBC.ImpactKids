using System.Globalization;

namespace GSBC.ImpactKids.WASM.Features.Games;

/// <summary>
/// How a score is written on a screen. Multiplied points run to four and five digits, and
/// a room of children reading "15000" off a wall has to count the noughts - so they are
/// grouped. Invariant rather than the browser's locale, because a display in a hall has
/// whatever locale the machine was set up with and the room does not.
/// </summary>
public static class GameScoreFormat
{
    public static string Points(int points) => points.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>A gain or loss with its sign, as the wall shows it landing.</summary>
    public static string Delta(int delta) =>
        (delta > 0 ? "+" : "−") + Points(Math.Abs(delta));
}
