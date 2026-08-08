namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

/// <summary>
/// What a scored point is worth on screen. Scoring stays in ones - a leader taps once
/// for one point - and only the displays scale it, so one point reads as 1000.
/// <para>
/// Multipliers are per game so a game that scores twenty times as often as the rest can
/// be brought down to 100 without touching the night's other games.
/// </para>
/// </summary>
public static class GameMultipliers
{
    /// <summary>What a night uses until somebody says otherwise.</summary>
    public const int Default = 1000;

    /// <summary>1 shows the points exactly as they were scored, which is a valid choice.</summary>
    public const int Min = 1;

    /// <summary>
    /// Twenty games at a hundred points each still leaves an int with room to spare, and
    /// nothing past six digits fits on a wall anyway.
    /// </summary>
    public const int Max = 100_000;

    /// <summary>The multipliers worth offering as a tap rather than a typed number.</summary>
    public static readonly int[] Presets = [1, 10, 100, 1_000, 10_000];

    public static bool IsValid(int multiplier) => multiplier is >= Min and <= Max;

    public static int Normalise(int multiplier, int fallback = Default) =>
        IsValid(multiplier) ? multiplier : fallback;

    public static int? Normalise(int? multiplier) =>
        multiplier == null ? null : IsValid(multiplier.Value) ? multiplier : null;

    /// <summary>
    /// The multiplier a game runs at: its own if it has one, otherwise the nearest
    /// earlier game that does, otherwise the board's.
    /// <para>
    /// Walking backwards is what makes a new game follow the game before it without
    /// anything having to be written when the game is started - and a game started
    /// offline still lands on the same number as one started on another phone.
    /// </para>
    /// </summary>
    public static int For(int gameNumber, int boardMultiplier, Func<int, int?> overrideFor)
    {
        int fallback = Normalise(boardMultiplier);

        for (int number = gameNumber; number >= 1; number--)
        {
            int? own = Normalise(overrideFor(number));

            if (own != null)
                return own.Value;
        }

        return fallback;
    }

    /// <summary>
    /// Multiplier per game, index 0 being game 1. Worked out once per board rather than
    /// per team, because resolving one game walks the list behind it.
    /// </summary>
    public static int[] PerGame(int gamesPlayed, int boardMultiplier, Func<int, int?> overrideFor)
    {
        int[] multipliers = new int[Math.Max(gamesPlayed, 0)];

        int running = Normalise(boardMultiplier);

        for (int number = 1; number <= multipliers.Length; number++)
        {
            running = Normalise(overrideFor(number)) ?? running;

            multipliers[number - 1] = running;
        }

        return multipliers;
    }

    /// <summary>
    /// Points as the wall shows them. Clamped rather than allowed to wrap: a board that
    /// somehow reaches two billion should read as a silly number, not a negative one.
    /// </summary>
    public static int Multiply(int points, int multiplier) =>
        (int)Math.Clamp((long)points * Normalise(multiplier), int.MinValue, int.MaxValue);
}
