using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceItemRecordServices;

public partial class AttendanceItemRecordService
{
    public async Task<BasicResponse> BasicDelete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbAttendanceItemRecord? attendanceItemRecord = await db.AttendanceItemRecords
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (attendanceItemRecord == null)
            return BasicResponse.WithError(AttendanceItemRecordNotFound);

        db.AttendanceItemRecords.Remove(attendanceItemRecord);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}