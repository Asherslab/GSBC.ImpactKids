namespace GSBC.ImpactKids.Grpc.Data.Models.Attendance;

public class DbAttendanceItemType
{
    public required Guid Id { get; set; }

    public required string Label { get; set; }

    public required int? Reward { get; set; }

    public required bool RequiresReturning { get; set; }
}