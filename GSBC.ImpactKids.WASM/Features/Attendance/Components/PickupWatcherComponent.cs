using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Components;

/// <summary>
/// Base for the unauthenticated pickup wall. Reads a server streamed list of the children
/// who have been asked for and have not yet gone, so a press on the sign out desk reaches
/// the room as fast as the write lands rather than on the next poll. The stream cannot use
/// the signed in event stream - a screen on a wall has no cookie - so the push comes down
/// the anonymous gRPC route the list is read over.
/// <para>
/// The same shape as <see cref="Games.Components.ScoreboardWatcherComponent"/>, against a
/// different service: the pickup list deliberately carries people, so it is never routed
/// through the games display service (see
/// <c>docs/work/2026-08-pickup-requests-and-activity-log.md</c>).
/// </para>
/// </summary>
public abstract class PickupWatcherComponent : ComponentBase, IAsyncDisposable
{
    /// <summary>Backoff for a dropped stream. Nothing on the wall is worth hammering for.</summary>
    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(15);

    [Parameter]
    public Guid? ServiceId { get; set; }

    [Inject]
    public required IAttendancePickupDisplayService PickupService { get; set; }

    protected PickupDisplayResponse? Pickups { get; private set; }

    private CancellationTokenSource? _watchTokenSource;

    /// <summary>
    /// Called on the render thread for every list that arrives, before the redraw, so a
    /// screen can work out which names are new while it still has the previous list.
    /// </summary>
    protected abstract void OnPickupsReceived(PickupDisplayResponse pickups);

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
                await foreach (PickupDisplayResponse pickups in PickupService.WatchPickups(
                                   new PickupDisplayRequest { ServiceId = ServiceId },
                                   token
                               ).WithCancellation(token))
                {
                    // A list arrived, so the connection is good - forget the backoff.
                    delay = MinRetryDelay;

                    await InvokeAsync(() =>
                        {
                            OnPickupsReceived(pickups);
                            Pickups = pickups;

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
                // Keep the last good list on screen rather than blanking the wall.
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
