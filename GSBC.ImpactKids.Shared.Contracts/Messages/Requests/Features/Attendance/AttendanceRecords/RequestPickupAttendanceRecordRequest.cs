namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class RequestPickupAttendanceRecordRequest
{
    public required Guid Id { get; init; }

    /// <summary>false clears the request — the same button, pressed again.</summary>
    public required bool Requested { get; init; }
}
