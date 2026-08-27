using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Components.Individual;

/// <summary>
/// One press: "a parent is here for this child". No dialog, no navigation, no confirmation step.
/// <para>
/// It is deliberately <em>not</em> a toggle on one tap target. Requesting is the big control;
/// once requested, that control becomes inert and informative and clearing moves to a small,
/// separate one. Two leaders work this desk at once, so the moment a request lands the same row
/// on the other phone would otherwise be showing a clear button where a request button was -
/// and it gets tapped, because a parent is standing in front of them. The child's name then
/// leaves the wall and nobody at the desk knows.
/// </para>
/// </summary>
public partial class PickupRequestButton
{
    /// <summary>The live record for this child tonight, or null when they have none.</summary>
    [Parameter]
    public AttendanceRecord? Record { get; set; }

    private bool _busy;

    /// <summary>
    /// Derived in <see cref="RetrieveLabels" />, not in the markup - a property that formats on
    /// every render runs once per row per frame.
    /// </summary>
    private string _requestedAgo = "";

    private string  _requestedAt = "";
    private string? _requestedBy;

    private string _signedOutAt = "";

    private CancellationTokenSource? _ticker;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Who took the request, for the same reason the activity log names an actor: the second
        // leader needs to know this was already handled, and by whom, before touching it.
        HandleSubscriptionDisposal(UsersStore, RetrieveLabels);

        RetrieveLabels();

        // "12m ago" goes stale on its own, and this page can sit open on a phone for an hour.
        // The loop is started once and only ever re-derives strings, so it costs a render a minute.
        _ticker = new CancellationTokenSource();
        _ = TickAsync(_ticker.Token);

        await UsersStore.RefreshAll();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        RetrieveLabels();
    }

    private async Task TickAsync(CancellationToken token)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(30));

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                if (Record is { AwaitingPickup: true })
                    RetrieveLabels();
            }
        }
        catch (OperationCanceledException)
        {
            // the component went away mid-wait
        }
    }

    private void RetrieveLabels()
    {
        if (Record?.LocalPickupRequested is { } requested)
        {
            _requestedAgo = Ago(requested);
            _requestedAt  = requested.ToString("h:mmtt").ToLowerInvariant();
        }
        else
        {
            _requestedAgo = "";
            _requestedAt  = "";
        }

        // Users is decoration, not a gate - the same rule the activity log follows. A control
        // that withholds "requested 3m ago" because it cannot name the leader is worse than one
        // that shows the time and leaves the actor off.
        _requestedBy = Record?.PickupRequestedUserId is { } actorId
            ? UsersStore.GetState().Entities.Data?
                .FirstOrDefault(x => x.Id == actorId)?.Name
            : null;

        _signedOutAt = Record?.LocalSignedOut is { } signedOut
            ? signedOut.ToString("h:mmtt").ToLowerInvariant()
            : "";

        StateHasChanged();
    }

    private static string Ago(DateTime local)
    {
        TimeSpan span = DateTime.Now - local;

        if (span < TimeSpan.FromMinutes(1))
            return "just now";

        if (span < TimeSpan.FromHours(1))
            return $"{(int)span.TotalMinutes}m ago";

        return $"{(int)span.TotalHours}h ago";
    }

    private Task Request() => SetRequested(true);

    private Task Clear() => SetRequested(false);

    private async Task SetRequested(bool requested)
    {
        if (Record == null || _busy)
            return;

        _busy = true;
        StateHasChanged();

        try
        {
            BasicResponse response = await AttendanceRecordsService.RequestPickup(
                new RequestPickupAttendanceRecordRequest
                {
                    Id        = Record.Id,
                    Requested = requested
                });

            if (response.HasErrorOrNull())
            {
                Snackbar.AddErrorResponse(response);
                return;
            }

            // RefreshEvent, NOT RefreshAll: RefreshAll goes through the executor's 30 minute
            // cache, so straight after a write it returns the response from before the write
            // and the control never settles. RefreshEvent invalidates the key first. Without
            // it this row only updates when the bus happens to push, which it does quickly
            // enough to look correct and slowly enough to be a bug.
            await AttendanceRecordsStore.RefreshEvent();
        }
        finally
        {
            _busy = false;
            RetrieveLabels();
        }
    }

    public override void Dispose()
    {
        _ticker?.Cancel();
        _ticker?.Dispose();
        _ticker = null;

        base.Dispose();
    }
}
