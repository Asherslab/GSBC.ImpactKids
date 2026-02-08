using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SignOutAttendanceRecordRequest
    : ReadRequestBase, IUpdateRequest<AttendanceRecord, SignOutAttendanceRecordRequest>
{
    public override string Id { get; set; } = null!;

    public static SignOutAttendanceRecordRequest FromEntity(AttendanceRecord entity)
    {
        SignOutAttendanceRecordRequest request = new()
        {
            Guid = entity.Id,
        };

        return request;
    }
}