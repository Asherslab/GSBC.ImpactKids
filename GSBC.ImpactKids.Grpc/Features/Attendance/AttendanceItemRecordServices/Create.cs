using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceItemRecords;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceItemRecordServices;

public partial class AttendanceItemRecordService
{
    public async Task<BasicReadResponse<Guid?>> Create(CreateAttendanceItemRecordRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbAttendanceRecord? attendanceRecord =
            await db.AttendanceRecords.FirstOrDefaultAsync(x => x.Id == request.AttendanceRecordId, token);

        if (attendanceRecord == null)
            return BasicReadResponse<Guid?>.WithError(AttendanceRecordNotFound);

        DbAttendanceItemType? attendanceItemType =
            await db.AttendanceItemTypes.FirstOrDefaultAsync(x => x.Id == request.AttendanceItemTypeId, token);

        if (attendanceItemType == null)
            return BasicReadResponse<Guid?>.WithError(AttendanceItemTypeNotFound);

        if (attendanceItemType.RequiresReturning && request.ItemReturned == null)
            return BasicReadResponse<Guid?>.WithError(AttendanceItemRecordReturnedRequired);

        DbAttendanceItemRecord attendanceItemRecord = new()
        {
            Id = Guid.Empty,

            RewardGiven = request.RewardGiven,
            ItemReturned = request.ItemReturned,

            AttendanceRecordId = attendanceRecord.Id,
            AttendanceItemTypeId = attendanceItemType.Id,
        };

        await db.AttendanceItemRecords.AddAsync(attendanceItemRecord, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = attendanceItemRecord.Id,
            Success = true
        };
    }
}