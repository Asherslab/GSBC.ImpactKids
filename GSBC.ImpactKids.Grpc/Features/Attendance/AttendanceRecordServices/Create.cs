using System.Security.Claims;
using Grpc.Core;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceRecordServices;

public partial class AttendanceRecordService
{
    public async Task<BasicReadResponse<Guid?>> Create(SignInAttendanceRecordRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbPerson? person = await db.People.FirstOrDefaultAsync(x => x.Id == request.PersonId, token);

        if (person == null)
            return BasicReadResponse<Guid?>.WithError(PersonNotFound);

        DbService? service = await db.Services.FirstOrDefaultAsync(x => x.Id == request.ServiceId, token);

        if (service == null)
            return BasicReadResponse<Guid?>.WithError(ServiceNotFound);

        string? userId = context.ServerCallContext?.GetHttpContext().User
            .FindFirstValue("UserId");

        if (userId == null)
            return BasicReadResponse<Guid?>.WithError(PermissionDenied);

        bool existingRecord = await db.AttendanceRecords
            .AnyAsync(x => x.ServiceId == service.Id &&
                           x.PersonId == person.Id &&
                           x.SignedOut == null &&
                           !x.Deleted,
                token
            );

        if (existingRecord)
            return BasicReadResponse<Guid?>.WithError(AttendanceRecordExists);
        
        DbAttendanceRecord attendanceRecord = new()
        {
            Id = Guid.Empty,

            PersonId = person.Id,
            SignedIn = DateTime.UtcNow,

            SignedInUserId = Guid.Parse(userId),
            ServiceId = service.Id
        };

        await db.AttendanceRecords.AddAsync(attendanceRecord, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = attendanceRecord.Id,
            Success = true
        };
    }
}