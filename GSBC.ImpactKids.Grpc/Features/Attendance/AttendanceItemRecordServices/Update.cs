using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceItemRecords;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceItemRecordServices;

public partial class AttendanceItemRecordService
{
    public async Task<BasicResponse> Update(UpdateAttendanceItemRecordRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbAttendanceItemRecord? attendanceItemRecord = await db.AttendanceItemRecords
            .Include(x => x.AttendanceItemType)
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (attendanceItemRecord == null)
            return BasicResponse.WithError(AttendanceRecordNotFound);

        if (request.ItemBrought.IsUpdated)
            attendanceItemRecord.ItemBrought = request.ItemBrought.Value;

        if (request.RewardGiven.IsUpdated)
            attendanceItemRecord.RewardGiven = request.RewardGiven.Value;

        if (request.ItemReturned.IsUpdated)
        {
            if (attendanceItemRecord.AttendanceItemType!.RequiresReturning && request.ItemReturned.Value == null)
                return BasicResponse.WithError(AttendanceItemRecordReturnedRequired);
            attendanceItemRecord.ItemReturned = request.ItemReturned.Value;
        }

        db.AttendanceItemRecords.Update(attendanceItemRecord);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}