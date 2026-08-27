using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;

[Service("gRPC/GSBC.ImpactKids.Attendance.Records")]
public interface IAttendanceRecordService
    : IBasicReadMultipleService<AttendanceRecord>,
        ICreateService<SignInAttendanceRecordRequest>,
        IUpdateService<SignOutAttendanceRecordRequest>,
        IBasicDeleteService<AttendanceRecord>
{
    /// <summary>
    /// Toggles "a parent has asked for this child". A named method rather than another
    /// generic base because <see cref="IUpdateService{T}"/> is already spent on the sign out.
    /// </summary>
    Task<BasicResponse> RequestPickup(
        RequestPickupAttendanceRecordRequest request,
        CallContext                          context = default
    );
}