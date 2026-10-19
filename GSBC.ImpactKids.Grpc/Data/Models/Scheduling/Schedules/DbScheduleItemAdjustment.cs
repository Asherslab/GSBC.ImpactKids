using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;

/// <summary>One "+5" pressed on the night, kept so a later look at overruns can see when and by whom.</summary>
public class DbScheduleItemAdjustment
{
    public required Guid           Id      { get; set; }
    public required Guid           ItemId  { get; set; }
    public required int            Minutes { get; set; }
    public required DateTimeOffset At      { get; set; }
    public          Guid?          UserId  { get; set; }

    [MapperIgnore]
    public DbScheduleItem? Item { get; set; }

    [MapperIgnore]
    public DbUser? User { get; set; }
}
