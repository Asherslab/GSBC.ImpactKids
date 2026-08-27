using System.Globalization;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Attendance;
using GSBC.ImpactKids.WASM.Features.Attendance.Components;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Pages;

/// <summary>
/// Unauthenticated wall display of the children who have been asked for and have not yet
/// gone. See <see cref="PickupWatcherComponent"/> for how the list reaches the screen, and
/// "The privacy decision" in
/// <c>docs/work/2026-08-pickup-requests-and-activity-log.md</c> for why a name is on a wall
/// at all.
/// <para>
/// Takes no input of any kind - signage has nobody standing at it. Everything an operator
/// needs lives on <c>/Attendance/Tool</c>.
/// </para>
/// </summary>
public partial class PickupDisplay
{
    /// <summary>
    /// Past this, a child has been forgotten rather than fetched, and the wall says so with
    /// a visual - the room should chase them.
    /// </summary>
    private static readonly TimeSpan Overdue = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often the waiting times are redrawn. The stream only pushes when the list
    /// changes, so without this a row's "3 mins" would sit there until somebody else was
    /// requested.
    /// </summary>
    private static readonly TimeSpan ClockTick = TimeSpan.FromSeconds(5);

    private IReadOnlyList<PickupRow> _rows = [];

    /// <summary>
    /// Every key currently on the wall. A key not in here when a list arrives is a name
    /// the room has not seen yet, which is the entrance animation's whole cue.
    /// </summary>
    private readonly HashSet<string> _onWall = [];

    /// <summary>
    /// Keys that arrived after the first list. Sticky for as long as the row is on the
    /// wall: the stream re-sends the same list every 30s as a keepalive, and recomputing
    /// "new" from the list alone would be fine, but a class that came and went would
    /// re-run the animation. Once a row has been marked as an arrival it stays marked, and
    /// because the row keeps its <c>@key</c> the browser runs the animation exactly once.
    /// </summary>
    private readonly HashSet<string> _arrived = [];

    /// <summary>
    /// False until the first list lands. The names already waiting when the screen boots
    /// are not arrivals - they get the quiet staggered fade the score board's rows get.
    /// </summary>
    private bool _seeded;

    private CancellationTokenSource? _clockTokenSource;

    /// <summary>One line on the wall: a child asked for, and the time they were asked for.</summary>
    private sealed record PickupRow(
        string   Key,
        string   Name,
        DateTime RequestedAt,
        int      Index,
        bool     IsNew
    );

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        StartClock();
    }

    /// <summary>
    /// The list arrives ordered longest wait first and is rendered in that order - it is
    /// the order the room should send children to the door in, so it is never re-sorted
    /// here.
    /// </summary>
    protected override void OnPickupsReceived(PickupDisplayResponse pickups)
    {
        List<PickupRow> rows = [];
        HashSet<string> keys = [];

        int index = 0;

        foreach (PickupDisplayEntry entry in pickups.Waiting)
        {
            // Name plus the instant they were asked for. A child requested, sent home and
            // requested again gets a different key, so the wall animates them in a second
            // time - which is exactly what happened.
            string key = $"{entry.Name}@{entry.RequestedAt.Ticks}";

            keys.Add(key);

            if (_seeded && !_onWall.Contains(key))
                _arrived.Add(key);

            rows.Add(new PickupRow(key, entry.Name, entry.RequestedAt, index++, _arrived.Contains(key)));
        }

        // Rows that have gone are forgotten, so a name coming back later is an arrival
        // again rather than a row that quietly reappears.
        _onWall.Clear();
        _onWall.UnionWith(keys);
        _arrived.IntersectWith(keys);

        _seeded = true;
        _rows = rows;
    }

    /// <summary>
    /// Redraws the waiting times between pushes. Nothing else on this page changes on a
    /// tick, and the rows keep their keys, so no animation restarts.
    /// </summary>
    private void StartClock()
    {
        _clockTokenSource?.Cancel();
        _clockTokenSource = new CancellationTokenSource();
        CancellationToken token = _clockTokenSource.Token;

        _ = Task.Run(async () =>
            {
                try
                {
                    using PeriodicTimer timer = new(ClockTick);

                    while (await timer.WaitForNextTickAsync(token))
                        await InvokeAsync(StateHasChanged);
                }
                catch (OperationCanceledException)
                {
                    // The screen went away.
                }
            }, token
        );
    }

    /// <summary>
    /// Type size relative to the five name wall everything was designed around, so a busy
    /// end of night still fits on one screen instead of scrolling off it.
    /// </summary>
    private string RowScale =>
        Math.Clamp(5d / Math.Max(_rows.Count, 1), .32, 1).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary><c>RequestedAt</c> crosses the wire in UTC, so the clock it is measured against is too.</summary>
    private static TimeSpan Waited(PickupRow row)
    {
        TimeSpan waited = DateTime.UtcNow - DateTime.SpecifyKind(row.RequestedAt, DateTimeKind.Utc);

        return waited < TimeSpan.Zero ? TimeSpan.Zero : waited;
    }

    /// <summary>Whole minutes only - a wall is read at a glance, and seconds ticking on it are noise.</summary>
    private static string WaitText(PickupRow row)
    {
        int minutes = (int)Waited(row).TotalMinutes;

        return minutes switch
        {
            < 1 => "just now",
            1   => "1 min",
            _   => $"{minutes} mins"
        };
    }

    private static bool IsOverdue(PickupRow row) => Waited(row) >= Overdue;

    public override async ValueTask DisposeAsync()
    {
        if (_clockTokenSource is not null)
        {
            await _clockTokenSource.CancelAsync();
            _clockTokenSource.Dispose();
            _clockTokenSource = null;
        }

        await base.DisposeAsync();
    }
}
