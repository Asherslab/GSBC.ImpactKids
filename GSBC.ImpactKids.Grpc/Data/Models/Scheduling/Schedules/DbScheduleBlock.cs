using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;

/// <summary>A stretch of the night. One lane means everybody together; two or more is a split.</summary>
public class DbScheduleBlock
{
    public required Guid Id         { get; set; }
    public required Guid ScheduleId { get; set; }
    public required int  Position   { get; set; }

    [MapperIgnore]
    public DbSchedule? Schedule { get; set; }

    [MapperIgnore]
    public List<DbScheduleLane> Lanes { get; set; } = [];
}
