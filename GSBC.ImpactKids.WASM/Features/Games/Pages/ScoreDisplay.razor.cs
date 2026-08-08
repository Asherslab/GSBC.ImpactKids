using System.Globalization;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Games;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Games.Pages;

/// <summary>
/// Unauthenticated wall display. Reads a server streamed scoreboard, so a tap on a
/// phone reaches the wall as fast as the write lands rather than on the next poll.
/// The stream cannot use the signed in event stream - this screen has no cookie - so
/// the push comes down the same anonymous gRPC route the board itself is read over.
/// </summary>
public partial class ScoreDisplay : IAsyncDisposable
{
    /// <summary>Backoff for a dropped stream. Nothing on the wall is worth hammering for.</summary>
    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(15);

    [Parameter]
    public Guid? ServiceId { get; set; }

    [Inject]
    public required IGameDisplayService DisplayService { get; set; }

    private GameScoreboardResponse? _board;

    private CancellationTokenSource? _watchTokenSource;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        StartWatching();
    }

    private void StartWatching()
    {
        _watchTokenSource?.Cancel();
        _watchTokenSource = new CancellationTokenSource();
        CancellationToken token = _watchTokenSource.Token;

        _ = Task.Run(() => WatchAsync(token), token);
    }

    private async Task WatchAsync(CancellationToken token)
    {
        TimeSpan delay = MinRetryDelay;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await foreach (GameScoreboardResponse board in DisplayService.WatchScoreboard(
                                   new GameScoreboardRequest { ServiceId = ServiceId },
                                   token
                               ).WithCancellation(token))
                {
                    // A board arrived, so the connection is good - forget the backoff.
                    delay = MinRetryDelay;

                    _board = board;

                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // Keep the last good board on screen rather than blanking the wall.
            }

            if (token.IsCancellationRequested)
                return;

            try
            {
                await Task.Delay(delay, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
        }
    }

    /// <summary>One line on the wall: a team, or the teams playing this game combined.</summary>
    private sealed record DisplayRow(string Label, string Colour, string Background, int Points);

    /// <summary>
    /// Combined teams share a line while the display is showing the current game - they
    /// score together, so two lines with the same number would just read as a mistake.
    /// The totals view always breaks them back apart, because the totals differ.
    /// </summary>
    private IReadOnlyList<DisplayRow> Rows
    {
        get
        {
            if (_board == null)
                return [];

            if (_board.Mode != GameDisplayMode.CurrentGame || !_board.CurrentGameHasAlliances)
                return _board.Teams
                    .Select(x => new DisplayRow(x.Name, x.Colour, x.Colour, x.DisplayPoints))
                    .ToList();

            return _board.Teams
                .GroupBy(x => x.AllianceGroup)
                .Select(group => new DisplayRow(
                        string.Join(" + ", group.Select(x => x.Name)),
                        group.First().Colour,
                        group.Count() == 1
                            ? group.First().Colour
                            : $"linear-gradient(135deg, {string.Join(", ", group.Select(x => x.Colour))})",
                        group.Max(x => x.DisplayPoints)
                    )
                )
                .OrderByDescending(x => x.Points)
                .ToList();
        }
    }

    /// <summary>
    /// Type size relative to the four team board everything was designed around, so a
    /// twenty team night still fits on one screen instead of scrolling off it.
    /// </summary>
    private string RowScale =>
        Math.Clamp(4d / Math.Max(Rows.Count, 1), .3, 1).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Bar length is relative to the leader, so the board reads at a distance.</summary>
    private string BarStyle(DisplayRow row)
    {
        int max = Rows.Count == 0 ? 0 : Rows.Max(x => Math.Max(x.Points, 0));

        int percent = max <= 0
            ? 0
            : (int)Math.Round(Math.Max(row.Points, 0) / (double)max * 100);

        return $"width: {percent}%;";
    }

    public async ValueTask DisposeAsync()
    {
        if (_watchTokenSource is not null)
        {
            await _watchTokenSource.CancelAsync();
            _watchTokenSource.Dispose();
            _watchTokenSource = null;
        }
    }
}
