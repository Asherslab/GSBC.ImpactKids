using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Components.Individual;

/// <summary>
/// One press to sign a child out, in place on the household page.
/// <para>
/// This replaces a three step page: check the person's details, confirm on a card that repeats
/// their name, then look at the attendance items. The steps were not earning their cost - the
/// leader pressing this has the child in front of them and does not need their date of birth
/// read back - and the last one was actively wrong, because the items were shown <em>after</em>
/// the sign out had already been written. The one thing worth stopping for arrived too late to
/// stop anything.
/// </para>
/// <para>
/// So it is the same shape as "sign out all": one press when nothing is outstanding, and when
/// something has to be handed back, the first press names it and the second commits. Quiet at
/// rest, loud only once a press has consequences.
/// </para>
/// </summary>
public partial class SignOutButton
{
    /// <summary>The latest record for this child tonight, or null when they have none.</summary>
    [Parameter]
    public AttendanceRecord? Record { get; set; }

    private bool _busy;
    private bool _confirming;

    private IReadOnlyList<OutstandingItem> _outstanding = [];

    /// <summary>
    /// Built here rather than in the markup - it walks two stores, and a property that does
    /// that runs once per row per frame.
    /// </summary>
    private string _outstandingLabel = "";

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(AttendanceItemRecordsStore, RetrieveOutstanding);
        HandleSubscriptionDisposal(AttendanceItemTypesStore, RetrieveOutstanding);

        RetrieveOutstanding();

        await Task.WhenAll(
            AttendanceItemRecordsStore.RefreshAll(),
            AttendanceItemTypesStore.RefreshAll()
        );
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        RetrieveOutstanding();
    }

    private void RetrieveOutstanding()
    {
        if (Record is not { SignedOut: null })
        {
            _outstanding      = [];
            _outstandingLabel = "";
            _confirming       = false;
            return;
        }

        ImmutableList<AttendanceItemRecord>? itemRecords = AttendanceItemRecordsStore.GetState().Entities.Data;
        ImmutableList<AttendanceItemType>?   itemTypes   = AttendanceItemTypesStore.GetState().Entities.Data;

        // One record, so the person's name is never needed - the card this sits in is already
        // theirs, and "Ethan C. - Phone" on Ethan's own tile is just noise.
        _outstanding = OutstandingItems.For([Record], itemRecords, itemTypes, _ => "");

        _outstandingLabel = _outstanding.Count switch
        {
            0 => "",
            1 => $"{_outstanding[0].Label} still to hand back",
            _ => $"{string.Join(", ", _outstanding.Select(x => x.Label))} still to hand back"
        };

        // Nothing left to hand back - drop the armed confirm rather than leaving a button
        // asking the leader to confirm a warning that no longer applies.
        if (_outstanding.Count == 0)
            _confirming = false;

        StateHasChanged();
    }

    private async Task Press()
    {
        if (Record == null || _busy)
            return;

        // Arm on the first press while anything is still outstanding. The list appears directly
        // above the button, so the second press is made with it in view.
        if (_outstanding.Count > 0 && !_confirming)
        {
            _confirming = true;
            StateHasChanged();
            return;
        }

        _confirming = false;
        _busy       = true;
        StateHasChanged();

        try
        {
            BasicResponse response = await AttendanceRecordsService.Update(
                new SignOutAttendanceRecordRequest
                {
                    Guid = Record.Id
                });

            if (response.HasErrorOrNull())
            {
                Snackbar.AddErrorResponse(response);
                return;
            }

            // RefreshEvent, NOT RefreshAll: RefreshAll goes through the executor's 30 minute
            // cache, so straight after a write it returns the response from before the write
            // and the row never settles.
            await AttendanceRecordsStore.RefreshEvent();
        }
        finally
        {
            _busy = false;
            RetrieveOutstanding();
        }
    }
}
