using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceItemRecordServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class AttendanceItemRecordService(
    GsbcDbContext                                            db,
    IEventService<AttendanceItemRecord>                      eventService,
    IConverter<DbAttendanceItemRecord, AttendanceItemRecord> converter
) : IAttendanceItemRecordService
{
}