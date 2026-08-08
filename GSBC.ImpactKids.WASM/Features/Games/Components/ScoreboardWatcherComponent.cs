using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Games;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Games.Components;

/// <summary>
/// Base for the unauthenticated wall screens. Reads a server streamed scoreboard, so a
/// tap on a phone reaches the wall as fast as the write lands rather than on the next
/// poll. The stream cannot use the signed in event stream - these screens have no
/// cookie - so the push comes down the same anonymous gRPC route the board is read over.
/// </summary>
public abstract class ScoreboardWatcherComponent : ComponentBase, IAsyncDisposable
{
    /// <summary>Backoff for a dropped stream. Nothing on the wall is worth hammering for.</summary>
    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(15);

    [Parameter]
    public Guid? ServiceId { get; set; }

    [Inject]
    public required IGameDisplayService DisplayService { get; set; }

    protected GameScoreboardResponse? Board { get; private set; }

    private CancellationTokenSource? _watchTokenSource;

    /// <summary>
    /// Called on the render thread for every board that arrives, before the redraw, so a
    /// screen can work out what actually moved while it still has the previous board.
    /// </summary>
    protected abstract void OnBoardReceived(GameScoreboardResponse board);

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

                    await InvokeAsync(() =>
                        {
                            OnBoardReceived(board);
                            Board = board;

                            StateHasChanged();
                        }
                    );
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

    public virtual async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_watchTokenSource is not null)
        {
            await _watchTokenSource.CancelAsync();
            _watchTokenSource.Dispose();
            _watchTokenSource = null;
        }
    }
}
