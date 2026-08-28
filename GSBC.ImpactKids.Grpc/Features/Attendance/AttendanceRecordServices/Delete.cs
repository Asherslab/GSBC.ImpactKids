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

        // Only the column this operation owns - see the sign out for why the row is now
        // genuinely multi writer.
        db.Entry(attendanceRecord).Property(x => x.Deleted).IsModified = true;

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}