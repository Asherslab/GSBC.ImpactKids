namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;

public class DbScheduleItemTemplate
{
    public required Guid      Id          { get; set; }
    public required string    Name        { get; set; }
    public required int       Minutes     { get; set; }
    public          TimeSpan? StartsAt    { get; set; }
    public required bool      NeedsFillIn { get; set; }
    public          bool      IsSection   { get; set; }

    /// <summary>A placeholder filled in with a song - from the song library, or by name for one not in it yet. Always <see cref="NeedsFillIn"/>.</summary>
    public bool IsSong { get; set; }
}
