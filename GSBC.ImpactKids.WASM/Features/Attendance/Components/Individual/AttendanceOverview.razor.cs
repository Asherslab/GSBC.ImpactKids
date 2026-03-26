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

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(AttendanceRecordsStore, RetrieveAttendanceRecords);

        RetrieveAttendanceRecords();
        
        await Task.WhenAll(
            AttendanceRecordsStore.RefreshAll()
        );
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

    private int? AttendanceCount() => _attendanceRecords.Data?
        .DistinctBy(x => x.PersonId)
        .Count();

    private int? CurrentSignedInDistinctCount() => _attendanceRecords.Data?
        .DistinctBy(x => x.PersonId)
        .Count(x => x.LocalSignedOut == null);

    private int? CurrentSignedOutDistinctCount() => _attendanceRecords.Data?
        .DistinctBy(x => x.PersonId)
        .Count(x => x.LocalSignedOut != null);
}