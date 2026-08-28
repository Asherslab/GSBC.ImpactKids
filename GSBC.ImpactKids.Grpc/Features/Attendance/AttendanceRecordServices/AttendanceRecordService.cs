using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceRecordServices;

public partial class AttendanceRecordService(
    GsbcDbContext                                    db,
    IEventService<AttendanceRecord>                  eventService,
    IConverter<DbAttendanceRecord, AttendanceRecord> converter
) : IAttendanceRecordService;