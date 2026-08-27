using System.Security.Claims;
using Grpc.Core;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceRecordServices;

public partial class AttendanceRecordService
{
    /// <summary>
    /// A parent has arrived and asked for this child - or the same button has been pressed
    /// again to take that back. One press, no flow: the request is a convenience for the
    /// room, never a gate in front of the sign out.
    /// </summary>
    public async Task<BasicResponse> RequestPickup(
        RequestPickupAttendanceRecordRequest request,
        CallContext                          context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbAttendanceRecord? attendanceRecord = await db.AttendanceRecords
            .FirstOrDefaultAsync(x => x.Id == request.Id, token);

        if (attendanceRecord == null)
            return BasicResponse.WithError(AttendanceRecordNotFound);

        string? userId = context.ServerCallContext?.GetHttpContext().User
            .FindFirstValue("UserId");

        if (userId == null)
            return BasicResponse.WithError(PermissionDenied);

        // Already gone. Someone tapped a stale list; an error on the desk mid pickup helps
        // nobody, so this is a success that changes nothing.
        if (attendanceRecord.SignedOut != null)
            return new BasicResponse
            {
                Success = true
            };

        DateTimeOffset? requested = request.Requested ? DateTimeOffset.UtcNow : null;
        Guid?           requestedBy = request.Requested ? Guid.Parse(userId) : null;

        // The desk and the door are two writers on one row, so only the columns this
        // operation owns are marked - never db.Update, which would write back whatever
        // sign out state this read happened to see.
        bool changed = (attendanceRecord.PickupRequested != null) != request.Requested;

        if (changed)
        {
            attendanceRecord.PickupRequested = requested;
            attendanceRecord.PickupRequestedUserId = requestedBy;

            db.Entry(attendanceRecord).Property(x => x.PickupRequested).IsModified = true;
            db.Entry(attendanceRecord).Property(x => x.PickupRequestedUserId).IsModified = true;

            await db.SaveChangesAsync(token);
            await eventService.SendUpdatedEvent(token);
        }

        return new BasicResponse
        {
            Success = true
        };
    }
}
