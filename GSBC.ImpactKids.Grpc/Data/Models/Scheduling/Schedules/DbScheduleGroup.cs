using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;

/// <summary>"Prep–3", "4–6": the groups a schedule can split into. The label feed's <c>?group=</c> names one.</summary>
public class DbScheduleGroup
{
    public required Guid   Id         { get; set; }
    public required Guid   ScheduleId { get; set; }
    public required string Name       { get; set; }
    public required int    Position   { get; set; }

    [MapperIgnore]
    public DbSchedule? Schedule { get; set; }
}
