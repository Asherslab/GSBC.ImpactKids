using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;

[Service("GSBC.ImpactKids.Attendance.ItemTypes")]
public interface IAttendanceItemTypeService : IBasicReadMultipleService<AttendanceItemType>;