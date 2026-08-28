using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Components.Individual;

public partial class AttendanceOverview
{
    [Parameter]
    public Guid? ServiceId { get; set; }

    private AsyncData<ImmutableList<AttendanceRecord>> _attendanceRecords =
        AsyncData<ImmutableList<AttendanceRecord>>.NotAsked();

    private Guid? _lastServiceId;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(AttendanceRecordsStore, RetrieveAttendanceRecords);

        RetrieveAttendanceRecords();

        await Task.WhenAll(
            AttendanceRecordsStore.RefreshAll()
        );
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (ServiceId != _lastServiceId)
        {
            _lastServiceId = ServiceId;
            RetrieveAttendanceRecords();
        }
    }

    private void RetrieveAttendanceRecords()
    {
        AsyncData<ImmutableList<AttendanceRecord>> attendanceRecords = AttendanceRecordsStore.GetState().Entities;

        if (attendanceRecords.Data == null)
        {
            _attendanceRecords = _attendanceRecords.CopyStatus(attendanceRecords);
            StateHasChanged();
            return;
        }

        ImmutableList<AttendanceRecord> records = attendanceRecords.Data
            .Where(x =>
                !x.Deleted &&
                x.ServiceId == ServiceId
            )
            .ToImmutableList();

        _attendanceRecords = _attendanceRecords.ToSuccess(records);
        StateHasChanged();
    }

    /// <summary>
    /// One record per person - the <em>latest</em> one, which is the only one that says
    /// where the child is now.
    /// <para>
    /// <c>DistinctBy</c> keeps the <em>first</em> match in enumeration order, and the server
    /// returns records ordered by <c>SignedIn</c> ascending
    /// (<c>AttendanceRecordServices/ReadMultiple.cs</c>). So a child signed in, signed out
    /// by mistake, and signed back in was counted by their first record - signed out - and
    /// the number a leader reads to decide the building is empty said the child had gone
    /// while they were still in the room. <c>Family.razor.cs</c> already does it this way.
    /// </para>
    /// </summary>
    private IEnumerable<AttendanceRecord>? LatestPerPerson() => _attendanceRecords.Data?
        .GroupBy(x => x.PersonId)
        .Select(g => g.MaxBy(x => x.SignedIn)!);

    private int? AttendanceCount() => LatestPerPerson()?.Count();

    private int? CurrentSignedInDistinctCount() => LatestPerPerson()?
        .Count(x => x.LocalSignedOut == null);

    private int? CurrentSignedOutDistinctCount() => LatestPerPerson()?
        .Count(x => x.LocalSignedOut != null);
}