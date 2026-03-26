namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SignInAttendanceRecordRequest
{
    public required Guid PersonId { get; init; }

    public required Guid ServiceId { get; init; }
}