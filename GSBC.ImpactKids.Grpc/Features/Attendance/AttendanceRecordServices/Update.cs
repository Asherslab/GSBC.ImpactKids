using System.Security.Claims;
using Grpc.Core;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceRecordServices;

public partial class AttendanceRecordService
{
    public async Task<BasicResponse> Update(SignOutAttendanceRecordRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbAttendanceRecord? attendanceRecord = await db.AttendanceRecords
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (attendanceRecord == null)
            return BasicResponse.WithError(AttendanceRecordNotFound);

        string? userId = context.ServerCallContext?.GetHttpContext().User
            .FindFirstValue("UserId");

        if (userId == null)
            return BasicResponse.WithError(PermissionDenied);

        attendanceRecord.SignedOut = DateTime.UtcNow;
        attendanceRecord.SignedOutUserId = Guid.Parse(userId);

        db.AttendanceRecords.Update(attendanceRecord);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}