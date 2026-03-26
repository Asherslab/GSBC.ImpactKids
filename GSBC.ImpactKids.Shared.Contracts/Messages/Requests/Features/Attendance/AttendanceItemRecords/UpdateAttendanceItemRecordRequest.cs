using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceItemRecords;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateAttendanceItemRecordRequest
    : ReadRequestBase, IUpdateRequest<AttendanceItemRecord, UpdateAttendanceItemRecordRequest>
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<bool>  RewardGiven  { get; init; } = new();
    public DeltaUpdate<bool?> ItemReturned { get; init; } = new();

    public static UpdateAttendanceItemRecordRequest FromEntity(AttendanceItemRecord entity)
    {
        UpdateAttendanceItemRecordRequest request = new()
        {
            Guid = entity.Id,
        };

        request.RewardGiven.SetInitialValue(entity.RewardGiven);
        request.ItemReturned.SetInitialValue(entity.ItemReturned);

        return request;
    }
}