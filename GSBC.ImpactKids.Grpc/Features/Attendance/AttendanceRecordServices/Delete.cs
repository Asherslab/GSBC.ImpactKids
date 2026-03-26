using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceRecordServices;

public partial class AttendanceRecordService
{
    public async Task<BasicResponse> BasicDelete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbAttendanceRecord? attendanceRecord = await db.AttendanceRecords
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (attendanceRecord == null)
            return BasicResponse.WithError(AttendanceRecordNotFound);

        attendanceRecord.Deleted = true;

        db.AttendanceRecords.Update(attendanceRecord);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}