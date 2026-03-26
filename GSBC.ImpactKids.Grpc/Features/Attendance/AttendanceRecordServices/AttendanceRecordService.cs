using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceRecordServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class AttendanceRecordService(
    GsbcDbContext                                    db,
    IEventService<AttendanceRecord>                  eventService,
    IConverter<DbAttendanceRecord, AttendanceRecord> converter
) : IAttendanceRecordService;