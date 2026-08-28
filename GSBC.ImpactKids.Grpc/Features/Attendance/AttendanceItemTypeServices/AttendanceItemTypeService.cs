using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceItemTypeServices;

public partial class AttendanceItemTypeService(
    GsbcDbContext                                        db,
    IConverter<DbAttendanceItemType, AttendanceItemType> converter
) : IAttendanceItemTypeService;