using System.Globalization;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Games;
using GSBC.ImpactKids.WASM.Features.Games.Components;

namespace GSBC.ImpactKids.WASM.Features.Games.Pages;

/// <summary>
/// Unauthenticated wall display of the live standings. See
/// <see cref="ScoreboardWatcherComponent"/> for how the board reaches the screen.
/// </summary>
public partial class ScoreDisplay
{
    private IReadOnlyList<DisplayRow> _rows = [];

    /// <summary>
    /// Points as of the last board, per row key. A row that moves gets a one off pop and
    /// a floating delta, which is the only cue on a wall that anything just happened.
    /// </summary>
    private readonly Dictionary<string, int> _lastPoints = [];

    /// <summary>Bumped whenever a row's points move, purely to restart its CSS animation.</summary>
    private readonly Dictionary<string, int> _bumps = [];

    protected override void OnBoardReceived(GameScoreboardResponse board) => _rows = BuildRows(board);

    /// <summary>
    /// One line on the wall: a team, or the teams playing this game combined.
    /// <para>
    /// <see cref="Key"/> holds the line's DOM position steady across boards while
    /// <see cref="Rank"/> moves it, so overtaking reads as one row sliding past another.
    /// </para>
    /// </summary>
    private sealed record DisplayRow(
        string Key,
        string Label,
        string Colour,
        string Background,
        int    Points,
        int    Rank,
        bool   IsLeader,
        int    Delta,
        int    Bump
    );

    /// <summary>
    /// Combined teams share a line while the display is showing the current game - they
    /// score together, so two lines with the same number would just read as a mistake.
    /// The totals view always breaks them back apart, because the totals differ.
    /// </summary>
    private IReadOnlyList<DisplayRow> BuildRows(GameScoreboardResponse board)
    {
        List<(string Key, string Label, string Colour, string Background, int Points)> lines;

        if (board.Mode != GameDisplayMode.CurrentGame || !board.CurrentGameHasAlliances)
            lines = board.Teams
                .Select(x => (
                        Key: $"t{x.TeamIndex}",
                        Label: x.Name,
                        x.Colour,
                        Background: x.Colour,
                        Points: x.DisplayPoints
                    )
                )
                .ToList();
        else
            lines = board.Teams
                .GroupBy(x => x.AllianceGroup)
                .Select(group => (
                        Key: $"a{string.Join("_", group.Select(x => x.TeamIndex).Order())}",
                        Label: string.Join(" + ", group.Select(x => x.Name)),
                        group.First().Colour,
                        Background: group.Count() == 1
                            ? group.First().Colour
                            : $"linear-gradient(135deg, {string.Join(", ", group.Select(x => x.Colour))})",
                        Points: group.Max(x => x.DisplayPoints)
                    )
                )
                .ToList();

        // Ranked separately from the render order - the DOM order has to stay put for the
        // rows to animate between placings rather than jump.
        List<string> ranked = lines
            .OrderByDescending(x => x.Points)
            .Select(x => x.Key)
            .ToList();

        // Everyone on the top score is a leader. Crowning only the first would make a
        // tie look like a lead, which is exactly the thing the wall must not get wrong.
        int best = lines.Select(x => x.Points).DefaultIfEmpty(0).Max();

        List<DisplayRow> rows = [];

        foreach ((string Key, string Label, string Colour, string Background, int Points) line in lines)
        {
            bool known = _lastPoints.TryGetValue(line.Key, out int previous);
            int delta = known ? line.Points - previous : 0;

            if (delta != 0)
                _bumps[line.Key] = _bumps.GetValueOrDefault(line.Key) + 1;

            _lastPoints[line.Key] = line.Points;

            rows.Add(new DisplayRow(
                    line.Key,
                    line.Label,
                    line.Colour,
                    line.Background,
                    line.Points,
                    ranked.IndexOf(line.Key),
                    line.Points == best && best > 0,
                    delta,
                    _bumps.GetValueOrDefault(line.Key)
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// Type size relative to the four team board everything was designed around, so a
    /// twenty team night still fits on one screen instead of scrolling off it.
    /// </summary>
    private string RowScale =>
        Math.Clamp(4d / Math.Max(_rows.Count, 1), .3, 1).ToString("0.00", CultureInfo.InvariantCulture);

    private static string RowStyle(DisplayRow row) =>
        $"--team: {row.Colour}; --team-bar: {row.Background}; --rank: {row.Rank};";

    /// <summary>Bar length is relative to the leader, so the board reads at a distance.</summary>
    private string BarStyle(DisplayRow row)
    {
        int max = _rows.Count == 0 ? 0 : _rows.Max(x => Math.Max(x.Points, 0));

        int percent = max <= 0
            ? 0
            : (int)Math.Round(Math.Max(row.Points, 0) / (double)max * 100);

        return $"width: {percent}%;";
    }
}
