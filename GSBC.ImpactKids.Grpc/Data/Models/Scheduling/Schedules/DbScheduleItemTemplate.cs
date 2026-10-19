namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;

public class DbScheduleItemTemplate
{
    public required Guid      Id          { get; set; }
    public required string    Name        { get; set; }
    public required int       Minutes     { get; set; }
    public          TimeSpan? StartsAt    { get; set; }
    public required bool      NeedsFillIn { get; set; }
    public          bool      IsSection   { get; set; }
}
