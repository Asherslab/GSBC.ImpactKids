using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;

/// <summary>
/// One press of Next on the night, kept so Back can undo exactly that press. <see cref="Before"/>
/// holds what it changed as it was before - each item's start and end, and the waiting row - as JSON.
/// One track's presses undo in order: the stage's (no <see cref="LaneId"/>), or one lane's.
/// </summary>
public class DbScheduleRunStep
{
    public required Guid           Id         { get; set; }
    public required Guid           ScheduleId { get; set; }
    public          Guid?          LaneId     { get; set; }
    public required DateTimeOffset At         { get; set; }
    public          Guid?          UserId     { get; set; }
    public required string         Before     { get; set; }

    [MapperIgnore]
    public DbSchedule? Schedule { get; set; }
}
