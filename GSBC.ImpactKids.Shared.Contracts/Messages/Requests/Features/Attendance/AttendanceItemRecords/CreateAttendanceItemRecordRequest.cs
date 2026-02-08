namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceItemRecords;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateAttendanceItemRecordRequest
{
    public required bool  ItemBrought  { get; init; }
    public required bool  RewardGiven  { get; init; }
    public required bool? ItemReturned { get; init; }

    public required Guid AttendanceRecordId   { get; init; }
    public required Guid AttendanceItemTypeId { get; init; }
}