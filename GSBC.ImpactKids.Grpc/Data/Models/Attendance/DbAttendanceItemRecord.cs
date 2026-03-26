using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Attendance;

public class DbAttendanceItemRecord
{
    public required Guid Id { get; set; }

    public required bool  RewardGiven  { get; set; }
    public required bool? ItemReturned { get; set; }

    public string? Notes { get; set; }

    // Relationships \\

    public required Guid AttendanceRecordId { get; set; }

    [MapperIgnore]
    public DbAttendanceRecord? AttendanceRecord { get; set; }

    public required Guid? AttendanceItemTypeId { get; set; }

    [MapperIgnore]
    public DbAttendanceItemType? AttendanceItemType { get; set; }
}