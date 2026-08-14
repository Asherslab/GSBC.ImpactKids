using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

namespace GSBC.ImpactKids.WASM.Features.Games;

/// <summary>
/// The running order of the end of night reveal, derived from the scores themselves.
/// <para>
/// Two screens have to agree on it: the phone driving the reveal knows only what step
/// number it is on, and the display on the wall has to turn that number back into
/// "game three's scores". Both build the order through here so a step means the same
/// thing on both, and the display clamps anything it cannot make sense of.
/// </para>
/// </summary>
public static class GameReveal
{
    /// <summary>How many places get their own moment before the board is left standing.</summary>
    private const int MaxPodium = 3;

    /// <summary>One team as the reveal needs it: who they are and what they won, round by round.</summary>
    public sealed record RevealTeam(
        int                Index,
        string             Name,
        string             Colour,
        IReadOnlyList<int> PerGame,
        int                Behaviour
    )
    {
        public int Total => PerGame.Sum() + Behaviour;
    }

    /// <summary>A block of points revealed in one go - a game, or the behaviour points.</summary>
    public sealed record RevealRound(string Title, bool IsBehaviour, IReadOnlyList<int> PointsByTeam);

    public enum RevealStage
    {
        /// <summary>Title card. Nothing is on the board yet.</summary>
        Intro,

        /// <summary>
        /// One round, start to finish: the name lands big in the middle of the screen,
        /// sits there, then flies up to the header as the points start counting on.
        /// </summary>
        Round,

        /// <summary>Reveals one place on the podium, counting down.</summary>
        Podium,

        /// <summary>The winner, and the end of the reveal.</summary>
        Champion
    }

    /// <param name="Round">Index into the round list, or -1 outside the rounds.</param>
    /// <param name="Place">Placing being revealed, 1 based, or 0 outside the podium.</param>
    public sealed record RevealStep(RevealStage Stage, int Round, int Place);

    /// <summary>
    /// The rounds to reveal: every game played, in order, then behaviour points if any
    /// were given.
    /// <para>
    /// A game nobody scored in still gets its turn. Skipping it saved a card and a board
    /// that does not move, at the cost of opening the reveal on "Game 2" - and a room
    /// that played five games counting four is a worse trade.
    /// </para>
    /// </summary>
    public static IReadOnlyList<RevealRound> Rounds(
        IReadOnlyList<string>     gameNames,
        IReadOnlyList<RevealTeam> teams
    )
    {
        List<RevealRound> rounds = [];

        for (int game = 0; game < gameNames.Count; game++)
        {
            int[] points = teams
                .Select(team => game < team.PerGame.Count ? team.PerGame[game] : 0)
                .ToArray();

            rounds.Add(new RevealRound(gameNames[game], IsBehaviour: false, points));
        }

        int[] behaviour = teams.Select(x => x.Behaviour).ToArray();

        if (behaviour.Any(x => x != 0))
            rounds.Add(new RevealRound("Behaviour points", IsBehaviour: true, behaviour));

        return rounds;
    }

    /// <summary>
    /// The placings the podium actually has, highest number first - the order they get
    /// revealed in.
    /// <para>
    /// Competition placing, so three teams level on points are all first and there is no
    /// second or third left to reveal. The podium is built from placings rather than from
    /// "the top three teams", because a placing can hold any number of teams.
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> PodiumPlacings(
        IReadOnlyList<RevealTeam>  teams,
        IReadOnlyList<RevealRound> rounds
    )
    {
        IReadOnlyDictionary<int, int> totals = TotalsThrough(teams, rounds, rounds.Count - 1);

        return teams
            .Select(team => PlaceOf(team, teams, totals))
            .Where(place => place <= MaxPodium)
            .Distinct()
            .OrderByDescending(place => place)
            .ToList();
    }

    /// <summary>Competition placing: everyone strictly ahead of this team, plus one.</summary>
    public static int PlaceOf(
        RevealTeam                    team,
        IReadOnlyList<RevealTeam>     teams,
        IReadOnlyDictionary<int, int> totals
    ) => teams.Count(x => totals[x.Index] > totals[team.Index]) + 1;

    /// <summary>
    /// Intro, then a card and a scoring moment per round, then the placings counted
    /// down one at a time. The last step is the winner, unless nobody scored at all.
    /// </summary>
    public static IReadOnlyList<RevealStep> Steps(int roundCount, IReadOnlyList<int> podiumPlacings)
    {
        List<RevealStep> steps = [new RevealStep(RevealStage.Intro, -1, 0)];

        for (int round = 0; round < roundCount; round++)
            steps.Add(new RevealStep(RevealStage.Round, round, 0));

        foreach (int place in podiumPlacings)
            steps.Add(new RevealStep(place == 1 ? RevealStage.Champion : RevealStage.Podium, -1, place));

        return steps;
    }

    /// <summary>
    /// Totals after every round up to and including <paramref name="throughRound"/>, keyed
    /// by team index. Pass -1 for "nothing revealed yet", which is a board of zeros.
    /// </summary>
    public static IReadOnlyDictionary<int, int> TotalsThrough(
        IReadOnlyList<RevealTeam>  teams,
        IReadOnlyList<RevealRound> rounds,
        int                        throughRound
    )
    {
        Dictionary<int, int> totals = teams.ToDictionary(x => x.Index, _ => 0);

        for (int round = 0; round <= throughRound && round < rounds.Count; round++)
            for (int team = 0; team < teams.Count; team++)
                totals[teams[team].Index] += rounds[round].PointsByTeam[team];

        return totals;
    }

    /// <summary>What the phone driving the reveal calls the step it is sitting on.</summary>
    public static string Describe(RevealStep step, IReadOnlyList<RevealRound> rounds) =>
        step.Stage switch
        {
            RevealStage.Intro => "Title card",
            RevealStage.Round => RoundTitle(step, rounds),
            RevealStage.Champion => "The winner 🥇",
            _ => $"{Ordinal(step.Place)} place"
        };

    private static string RoundTitle(RevealStep step, IReadOnlyList<RevealRound> rounds) =>
        step.Round >= 0 && step.Round < rounds.Count ? rounds[step.Round].Title : "Scores";

    /// <summary>
    /// "1st", "7th", "11th". One implementation, shared with placement scoring - the wall
    /// and the phone must word a placing identically.
    /// </summary>
    public static string Ordinal(int place) => GamePlacements.Ordinal(place);

    public static string Medal(int place) => place switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => $"{place}"
    };
}
