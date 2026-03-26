using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;

[Service("gRPC/GSBC.ImpactKids.Attendance.Records")]
public interface IAttendanceRecordService
    : IBasicReadMultipleService<AttendanceRecord>,
        ICreateService<SignInAttendanceRecordRequest>,
        IUpdateService<SignOutAttendanceRecordRequest>,
        IBasicDeleteService<AttendanceRecord>;