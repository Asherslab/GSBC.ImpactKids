using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceItemRecords;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;

[Service("gRPC/GSBC.ImpactKids.Attendance.ItemRecords")]
public interface IAttendanceItemRecordService
    : IBasicReadMultipleService<AttendanceItemRecord>,
        ICreateService<CreateAttendanceItemRecordRequest>,
        IUpdateService<UpdateAttendanceItemRecordRequest>,
        IBasicDeleteService<AttendanceItemRecord>;