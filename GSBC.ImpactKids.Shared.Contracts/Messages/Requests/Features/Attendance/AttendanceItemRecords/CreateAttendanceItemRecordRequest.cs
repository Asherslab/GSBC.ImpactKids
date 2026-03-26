namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceItemRecords;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateAttendanceItemRecordRequest
{
    public bool  RewardGiven  { get; set; }
    public bool? ItemReturned { get; set; }

    public Guid  AttendanceRecordId   { get; set; }
    public Guid? AttendanceItemTypeId { get; set; }
}