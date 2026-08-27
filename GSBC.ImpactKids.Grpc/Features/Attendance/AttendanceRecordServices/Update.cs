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

        // The desk and the door are two writers on one row now that a pickup can be
        // requested from another phone while this sign out is in flight. db.Update would
        // emit every column - including PickupRequested as it was read milliseconds ago -
        // and quietly undo a request that landed in between.
        db.Entry(attendanceRecord).Property(x => x.SignedOut).IsModified = true;
        db.Entry(attendanceRecord).Property(x => x.SignedOutUserId).IsModified = true;

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}