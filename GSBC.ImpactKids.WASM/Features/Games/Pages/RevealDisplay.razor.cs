using System.Globalization;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Games;
using GSBC.ImpactKids.WASM.Features.Games.Components;
using static GSBC.ImpactKids.WASM.Features.Games.GameReveal;

namespace GSBC.ImpactKids.WASM.Features.Games.Pages;

/// <summary>
/// Unauthenticated wall display for the end of night reveal. It takes no input of its
/// own - a screen on a wall has nobody standing at it - so the step it is showing comes
/// down the scoreboard stream from whoever is driving it on <see cref="GameScores"/>.
/// </summary>
public partial class RevealDisplay
{
    /// <summary>Enough to fill a wall without asking the browser to animate a thousand nodes.</summary>
    private const int ConfettiCount = 90;

    private IReadOnlyList<RevealTeam>  _teams  = [];
    private IReadOnlyList<RevealRound> _rounds = [];
    private IReadOnlyList<RevealStep>  _steps  = [];

    private IReadOnlyList<string> _confetti = [];

    /// <summary>
    /// Longest a round's count up may run. A game worth eighty points would otherwise
    /// hold the room for a minute.
    /// </summary>
    private static readonly TimeSpan MaxCountDuration = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Even a one tap round gets a proper climb. The count up is the moment of the round -
    /// a board that snaps to its answer in a second is over before a child has found their
    /// team on it.
    /// </summary>
    private static readonly TimeSpan MinCountDuration = TimeSpan.FromSeconds(2.5);

    /// <summary>
    /// How many *scored* points a round gets through in a second - taps, not the
    /// multiplied numbers on screen (see <see cref="Granularity"/>), so a round worth five
    /// taps takes the same time whether the wall shows it as 5 or as 5000.
    /// <para>
    /// Deliberately slow. This only sets how long the round runs for; the numbers
    /// themselves climb smoothly through every value in between, so a 1000x round reads as
    /// a scoreboard rolling up rather than as five jumps.
    /// </para>
    /// </summary>
    private const double PointsPerSecond = 4;

    private static readonly TimeSpan CountTick = TimeSpan.FromMilliseconds(40);

    /// <summary>
    /// How long the round's name sits in the middle of the screen before it flies up to
    /// the header and the points start climbing. Long enough for a room of children to
    /// read it out loud.
    /// </summary>
    private static readonly TimeSpan CardHold = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Step being shown, already clamped into what this screen can actually render. The
    /// board carries a raw number; nothing stops it naming a round this display has not
    /// been told about yet.
    /// </summary>
    private int _step;

    /// <summary>The step the count up currently on screen belongs to. -1 before the first.</summary>
    private int _countedStep = -1;

    /// <summary>
    /// What each team's total reads right now, which during a round is somewhere between
    /// the previous total and the new one. Empty means "show the real totals".
    /// </summary>
    private readonly Dictionary<int, int> _shown = [];

    /// <summary>
    /// The round's name is still sitting in the middle of the screen. The board behind it
    /// shows the standings as they were before this round, because they are.
    /// </summary>
    private bool _cardPhase;

    private CancellationTokenSource? _countSource;

    private bool Running => Board is { Success: true, Hidden: false, RevealStep: not null } && _steps.Count > 0;

    private RevealStep Current =>
        _steps.Count == 0 ? new RevealStep(RevealStage.Intro, -1, 0) : _steps[_step];

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _confetti = BuildConfetti();
    }

    protected override void OnBoardReceived(GameScoreboardResponse board)
    {
        _teams = board.Teams
            .OrderBy(x => x.TeamIndex)
            .Select(x => new RevealTeam(x.TeamIndex, x.Name, x.Colour, x.PerGamePoints, x.BehaviourPoints))
            .ToList();

        _rounds = Rounds(board.GameNames, _teams);
        _steps = _teams.Count == 0 ? [] : Steps(_rounds.Count, PodiumPlacings(_teams, _rounds));

        int step = _steps.Count == 0
            ? 0
            : Math.Clamp(board.RevealStep ?? 0, 0, _steps.Count - 1);

        // Keepalives re-send the same board. Only a real move of the step restarts the
        // count, or the numbers would spring back to the start every thirty seconds.
        if (step != _step || _countedStep != step)
        {
            _step = step;
            StartCount();
        }
    }

    /// <summary>
    /// Runs the numbers up from the previous totals to the new ones. Every team climbs at
    /// the same points per second, so the team that won least stops first and the one that
    /// won most is still going after everyone else has landed - and while they climb the
    /// rows re-rank, so a team overtaking another is watched rather than announced.
    /// </summary>
    private void StartCount()
    {
        _countSource?.Cancel();
        _countSource?.Dispose();
        _countSource = null;

        _countedStep = _step;
        _shown.Clear();
        _cardPhase = false;

        // Only a scoring step counts. Everything else is already at its final number.
        if (Current.Stage != RevealStage.Round)
            return;

        _cardPhase = true;

        IReadOnlyDictionary<int, int> from   = TotalsThrough(_teams, _rounds, Current.Round - 1);
        IReadOnlyDictionary<int, int> target = TotalsThrough(_teams, _rounds, Current.Round);

        int biggest = _teams
            .Select(x => Math.Abs(target[x.Index] - from[x.Index]))
            .DefaultIfEmpty(0)
            .Max();

        // Held at the previous totals for as long as the name is on screen. Nobody has
        // been told what this round was worth yet.
        foreach (RevealTeam team in _teams)
            _shown[team.Index] = from[team.Index];

        _countSource = new CancellationTokenSource();

        _ = RunRoundAsync(from, target, biggest, Granularity(Current.Round), _countSource.Token);
    }

    /// <summary>
    /// What one scored point is worth in this round, worked out from the numbers rather
    /// than told to us - the stream only ever carries multiplied points.
    /// <para>
    /// The highest common factor of the round's gains, so a 1000x round of 1000/3000/5000
    /// reads as three, one and five taps. It only paces the count up, so being wrong (a
    /// round of 2000 and 4000 looks like a 2000x round) just makes the climb a little
    /// quicker - and the totals still land on the real numbers.
    /// </para>
    /// </summary>
    private int Granularity(int round)
    {
        if (round < 0 || round >= _rounds.Count)
            return 1;

        int factor = 0;

        foreach (int gain in _rounds[round].PointsByTeam)
            factor = Gcd(factor, Math.Abs(gain));

        return Math.Max(factor, 1);
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
            (a, b) = (b, a % b);

        return a;
    }

    /// <summary>
    /// Holds on the name, then counts. One step, so the operator presses next once per
    /// round rather than twice.
    /// </summary>
    private async Task RunRoundAsync(
        IReadOnlyDictionary<int, int> from,
        IReadOnlyDictionary<int, int> target,
        int                           biggest,
        int                           granularity,
        CancellationToken             token
    )
    {
        try
        {
            await Task.Delay(CardHold, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _cardPhase = false;

        await InvokeAsync(StateHasChanged);

        // A round nobody scored in still gets its name and its moment - it just has
        // nothing to count afterwards.
        if (biggest == 0)
            return;

        double seconds = Math.Clamp(
            biggest / (double)granularity / PointsPerSecond,
            MinCountDuration.TotalSeconds,
            MaxCountDuration.TotalSeconds
        );

        // One pace for everyone, set by the largest haul - that is what staggers the
        // finishes without anyone appearing to count slower than anyone else.
        await CountAsync(from, target, biggest / seconds, token);
    }

    private async Task CountAsync(
        IReadOnlyDictionary<int, int> from,
        IReadOnlyDictionary<int, int> target,
        double                        pace,
        CancellationToken             token
    )
    {
        DateTime started = DateTime.UtcNow;

        using PeriodicTimer timer = new(CountTick);

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                double travelled = (DateTime.UtcNow - started).TotalSeconds * pace;

                bool done = true;

                foreach (RevealTeam team in _teams)
                {
                    int gain = target[team.Index] - from[team.Index];
                    int step = Math.Min((int)travelled, Math.Abs(gain));

                    _shown[team.Index] = from[team.Index] + Math.Sign(gain) * step;

                    if (step < Math.Abs(gain))
                        done = false;
                }

                await InvokeAsync(StateHasChanged);

                if (done)
                    return;
            }
        }
        catch (OperationCanceledException)
        {
            // Stepped somewhere else mid count. The new step has its own numbers.
        }
    }

    /// <summary>
    /// This round was played and awarded nothing. The board has to do something about it
    /// once the title docks, or the screen just appears to have stopped.
    /// </summary>
    private bool RoundIsBlank =>
        Current.Stage == RevealStage.Round &&
        !_cardPhase &&
        Current.Round < _rounds.Count &&
        _rounds[Current.Round].PointsByTeam.All(x => x == 0);

    private string RoundTitle =>
        Current.Round >= 0 && Current.Round < _rounds.Count ? _rounds[Current.Round].Title : "";

    /// <summary>Rounds already added to the board at the step being shown.</summary>
    private int RevealedThrough => Current.Stage switch
    {
        RevealStage.Intro => -1,

        // While the name is still on screen the round has not landed yet.
        RevealStage.Round => _cardPhase ? Current.Round - 1 : Current.Round,

        _ => _rounds.Count - 1
    };

    /// <summary>
    /// Type size relative to the four team board everything was designed around, so a
    /// twenty team night still fits on one screen instead of scrolling off it.
    /// </summary>
    private string RowScale =>
        Math.Clamp(4d / Math.Max(_teams.Count, 1), .3, 1).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// One team's line on the board at the step being shown.
    /// <para>
    /// <see cref="Rank"/> is where the row sits and is always unique; <see cref="Place"/>
    /// is what it is called and is shared by everyone on the same score. Two teams level
    /// on points are both second, and the board has to say so.
    /// </para>
    /// </summary>
    private sealed record RevealRow(
        RevealTeam Team,
        int        Total,
        int        Gain,
        int        Rank,
        int        Place,
        bool       IsLeader,
        int        Percent
    );

    private IReadOnlyList<RevealRow> Rows
    {
        get
        {
            // Mid count the board runs on the numbers on screen, not the final ones -
            // that is what makes a team climb past another as its total goes by.
            IReadOnlyDictionary<int, int> totals = _shown.Count > 0
                ? _shown
                : TotalsThrough(_teams, _rounds, RevealedThrough);

            // Only a scoring step shows what was just won - on a card the round has not
            // landed yet, and by the podium it is old news.
            int[]? gains = Current.Stage == RevealStage.Round && Current.Round < _rounds.Count
                ? _rounds[Current.Round].PointsByTeam.ToArray()
                : null;

            int max   = totals.Values.DefaultIfEmpty(0).Max();
            int scale = BarScale(totals);

            List<int> ranked = _teams
                .OrderByDescending(x => totals[x.Index])
                .ThenBy(x => x.Index)
                .Select(x => x.Index)
                .ToList();

            return _teams
                .Select((team, position) => new RevealRow(
                        team,
                        totals[team.Index],
                        gains != null && position < gains.Length ? gains[position] : 0,
                        ranked.IndexOf(team.Index),
                        PlaceOf(team, _teams, totals),
                        totals[team.Index] == max && max > 0,
                        scale <= 0 ? 0 : (int)Math.Round(Math.Max(totals[team.Index], 0) / (double)scale * 100)
                    )
                )
                .ToList();
        }
    }

    /// <summary>
    /// What a full width bar is worth: the leader's total <em>once this round has landed</em>,
    /// not the leader's total right now.
    /// <para>
    /// Measuring against the running leader made every bar shrink as the biggest number
    /// climbed - a team that had just won nothing watched its bar get shorter, which to a
    /// room of children reads as points being taken away. Giving the board the room the
    /// round needs before it starts means bars only ever grow while the numbers count.
    /// The room appears when the round's name card does, before anything is counting.
    /// </para>
    /// </summary>
    private int BarScale(IReadOnlyDictionary<int, int> shown)
    {
        int through = Current.Stage == RevealStage.Round ? Current.Round : RevealedThrough;

        int landed = TotalsThrough(_teams, _rounds, through).Values.DefaultIfEmpty(0).Max();

        // A round can take points off. Never let a bar out-run its own track.
        return Math.Max(landed, shown.Values.DefaultIfEmpty(0).Max());
    }

    /// <summary>
    /// One step of the podium: a placing, whether it has been revealed yet, and whether
    /// anyone else is on it. <see cref="Shared"/> drives the wording - a room full of kids
    /// looking at two gold medals needs to be told, in words, that it is a tie.
    /// </summary>
    private sealed record PodiumSlot(int Place, RevealTeam Team, int Total, bool Shown, bool Shared = false);

    private IReadOnlyList<PodiumSlot> Podium
    {
        get
        {
            IReadOnlyDictionary<int, int> totals = TotalsThrough(_teams, _rounds, _rounds.Count - 1);

            // Everyone in a podium placing, however many that is. Taking the top three
            // teams instead would drop the fourth of four teams tied for first.
            List<PodiumSlot> slots = _teams
                .Select(team => new PodiumSlot(PlaceOf(team, _teams, totals), team, totals[team.Index], false))
                .Where(slot => slot.Place <= 3)
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.Team.Index)
                .ToList();

            slots = slots
                // Counted down, so a placing is on screen once its own step has passed.
                .Select(slot => slot with
                    {
                        Shown = Current.Place > 0 && slot.Place >= Current.Place,
                        Shared = slots.Count(x => x.Place == slot.Place) > 1
                    }
                )
                .ToList();

            // Second, first, third - a podium reads from its middle, and the tallest
            // block belongs in the centre.
            return slots
                .OrderBy(x => x.Place switch { 2 => 0, 1 => 1, _ => 2 })
                .ToList();
        }
    }

    /// <summary>
    /// Says out loud what the medals only imply. A tie is rare enough that nobody in the
    /// room will be expecting it, which is exactly why it has to be spelled out.
    /// </summary>
    private string PodiumTitle
    {
        get
        {
            IReadOnlyList<PodiumSlot> slots = Podium;

            if (Current.Stage == RevealStage.Champion)
            {
                int winners = slots.Count(x => x.Place == 1);

                return winners > 1 ? $"It's a tie - {winners} winners!" : "Tonight's winner";
            }

            bool shared = slots.Any(x => x.Place == Current.Place && x.Shared);

            return shared
                ? $"Tied for {Ordinal(Current.Place)}"
                : $"In {Ordinal(Current.Place)} place";
        }
    }

    /// <summary>
    /// Fixed at startup rather than per render: the pieces only ever fall once, and
    /// re-rolling them mid animation would make the confetti stutter.
    /// </summary>
    private IReadOnlyList<string> BuildConfetti()
    {
        Random random = new(20260808);

        string[] colours = ["#f87171", "#facc15", "#4ade80", "#60a5fa", "#c084fc", "#fb923c", "#ffffff"];

        return Enumerable.Range(0, ConfettiCount)
            .Select(_ =>
                {
                    string left     = Number(random.Next(0, 101));
                    string delay    = Number(random.Next(0, 3500) / 1000d);
                    string duration = Number(2.5 + random.Next(0, 2500) / 1000d);
                    string drift    = Number(random.Next(-25, 26));
                    string spin     = Number(random.Next(360, 1440));
                    string size     = Number(0.6 + random.Next(0, 90) / 100d);

                    return $"left: {left}%; " +
                           $"--delay: {delay}s; " +
                           $"--duration: {duration}s; " +
                           $"--drift: {drift}vw; " +
                           $"--spin: {spin}deg; " +
                           $"--size: {size}vmin; " +
                           $"background: {colours[random.Next(colours.Length)]};";
                }
            )
            .ToList();
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public override async ValueTask DisposeAsync()
    {
        if (_countSource is not null)
        {
            await _countSource.CancelAsync();
            _countSource.Dispose();
            _countSource = null;
        }

        await base.DisposeAsync();
    }
}
