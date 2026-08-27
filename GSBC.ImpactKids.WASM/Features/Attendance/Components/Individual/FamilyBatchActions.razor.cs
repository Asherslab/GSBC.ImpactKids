using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Components.Individual;

/// <summary>
/// "Request all" and "Sign out all" for one household.
/// <para>
/// Parents arrive per household and the app is built per child, so a parent of three costs
/// three searches and three passes through the stepper - forty times over in the last ten
/// minutes of a night. These do the whole household in one press.
/// </para>
/// <para>
/// <b>Sign out all is only safe because it surfaces what has to be handed back.</b> The
/// per-child stepper has a step for returning bags and phones
/// (<c>AttendanceItemRecordDisplay ShowReturnError</c>); a batch that skipped it silently
/// would send children home without their things. So the outstanding items are listed on
/// screen, and while any remain the button needs a second, deliberate press.
/// </para>
/// </summary>
public partial class FamilyBatchActions
{
    /// <summary>Everyone in the household, including the person whose page this is.</summary>
    [Parameter]
    public IReadOnlyList<Person>? Members { get; set; }

    /// <summary>Latest record this service per person, signed out or not.</summary>
    [Parameter]
    public IReadOnlyDictionary<Guid, AttendanceRecord>? Records { get; set; }

    /// <summary>Raised once a batch has finished, so the page can re-read.</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    private bool _busy;
    private bool _confirmingSignOut;

    private IReadOnlyList<AttendanceRecord> _requestable = [];
    private IReadOnlyList<AttendanceRecord> _signOutable = [];
    private IReadOnlyList<OutstandingItem>  _outstanding = [];

    /// <summary>One thing a child is still holding, named well enough to go and find it.</summary>
    private sealed record OutstandingItem(string PersonName, string Label);

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(AttendanceItemRecordsStore, RetrieveBatches);
        HandleSubscriptionDisposal(AttendanceItemTypesStore, RetrieveBatches);

        RetrieveBatches();

        await Task.WhenAll(
            AttendanceItemRecordsStore.RefreshAll(),
            AttendanceItemTypesStore.RefreshAll()
        );
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        RetrieveBatches();
    }

    /// <summary>
    /// Derived here rather than in the markup - these walk three stores, and a property that
    /// does that runs on every render.
    /// </summary>
    private void RetrieveBatches()
    {
        if (Members == null || Records == null)
        {
            _requestable = [];
            _signOutable = [];
            _outstanding = [];
            return;
        }

        List<AttendanceRecord> live = Members
            .Select(x => Records.GetValueOrDefault(x.Id))
            .Where(x => x is { SignedOut: null })
            .Select(x => x!)
            .ToList();

        _signOutable = live;

        // Already-requested children are not "requestable" - otherwise a household of three
        // with two already asked for offers "Request all 1", which is noise beside the row
        // button that does the same thing.
        _requestable = live
            .Where(x => !x.AwaitingPickup)
            .ToList();

        _outstanding = BuildOutstanding(live);

        // Nothing left to hand back - drop the armed confirm rather than leaving a button
        // asking the leader to confirm a warning that no longer applies.
        if (_outstanding.Count == 0)
            _confirmingSignOut = false;

        StateHasChanged();
    }

    /// <summary>
    /// Outstanding means the item type says it must come back and the record does not say it
    /// has. An item type that has not loaded is treated as <em>not</em> outstanding: the
    /// panel exists to name specific things, and "something, possibly" sends nobody anywhere.
    /// </summary>
    private IReadOnlyList<OutstandingItem> BuildOutstanding(IReadOnlyList<AttendanceRecord> live)
    {
        ImmutableList<AttendanceItemRecord>? itemRecords = AttendanceItemRecordsStore.GetState().Entities.Data;
        ImmutableList<AttendanceItemType>?   itemTypes   = AttendanceItemTypesStore.GetState().Entities.Data;

        if (itemRecords == null || itemTypes == null || Members == null)
            return [];

        Dictionary<Guid, AttendanceItemType> typesById = itemTypes.ToDictionary(x => x.Id);
        Dictionary<Guid, string>             nameByPersonId = Members.ToDictionary(x => x.Id, DisplayNameOf);

        List<OutstandingItem> outstanding = [];

        foreach (AttendanceRecord record in live)
        {
            foreach (AttendanceItemRecord item in itemRecords.Where(x => x.AttendanceRecordId == record.Id))
            {
                if (item.ItemReturned == true)
                    continue;

                if (item.AttendanceItemTypeId == null ||
                    !typesById.TryGetValue(item.AttendanceItemTypeId.Value, out AttendanceItemType? type) ||
                    !type.RequiresReturning)
                    continue;

                outstanding.Add(new OutstandingItem(
                    nameByPersonId.GetValueOrDefault(record.PersonId, "Someone"),
                    type.Label
                ));
            }
        }

        return outstanding;
    }

    private static string DisplayNameOf(Person person) =>
        string.IsNullOrWhiteSpace(person.LastName)
            ? person.FirstName
            : $"{person.FirstName} {person.LastName[0]}.";

    private async Task RequestAll()
    {
        if (_busy)
            return;

        await RunBatch(_requestable, record => AttendanceRecordsService.RequestPickup(
            new RequestPickupAttendanceRecordRequest
            {
                Id        = record.Id,
                Requested = true
            }));
    }

    private async Task SignOutAll()
    {
        if (_busy)
            return;

        // Arm on the first press while anything is still outstanding. The list is above the
        // button, so the second press is made with it in view.
        if (_outstanding.Count > 0 && !_confirmingSignOut)
        {
            _confirmingSignOut = true;
            StateHasChanged();
            return;
        }

        _confirmingSignOut = false;

        await RunBatch(_signOutable, record => AttendanceRecordsService.Update(
            new SignOutAttendanceRecordRequest
            {
                Guid = record.Id
            }));
    }

    /// <summary>
    /// Sequential, not <c>Task.WhenAll</c>. These are writes to rows a second phone may also
    /// be touching, and a household is three or four of them - there is nothing to win by
    /// firing them together, and a partial failure is far easier to report in order.
    /// </summary>
    private async Task RunBatch(
        IReadOnlyList<AttendanceRecord>        records,
        Func<AttendanceRecord, Task<BasicResponse>> operation
    )
    {
        _busy = true;
        StateHasChanged();

        int failed = 0;

        try
        {
            foreach (AttendanceRecord record in records)
            {
                BasicResponse response = await operation(record);

                if (response.HasErrorOrNull())
                    failed++;
            }

            // One message for the batch. A snackbar per child on a busy desk is noise the
            // leader learns to dismiss without reading.
            if (failed > 0)
                Snackbar.Add(
                    failed == records.Count
                        ? "None of them went through. Try each child instead."
                        : $"{failed} of {records.Count} did not go through - check each child.",
                    Severity.Error);
        }
        finally
        {
            _busy = false;

            await AttendanceRecordsStore.RefreshAll();
            await OnChanged.InvokeAsync();

            RetrieveBatches();
        }
    }
}
