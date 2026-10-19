using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;

public class DbScheduleItem
{
    public required Guid   Id       { get; set; }
    public required Guid   LaneId   { get; set; }
    public required int    Position { get; set; }
    public required string Name     { get; set; }

    /// <summary>What a placeholder was filled in with - the "Way Maker" in "Song 1 — Way Maker".</summary>
    public string? FillIn { get; set; }

    public required bool NeedsFillIn { get; set; }

    /// <summary>The template it was added from, kept for comparing like with like across services.</summary>
    public Guid? ItemTemplateId { get; set; }

    [MapperIgnore]
    public DbScheduleItemTemplate? ItemTemplate { get; set; }

    /// <summary>The length it was planned with.</summary>
    public required int PlannedMinutes { get; set; }

    /// <summary>The sum of the time added or taken away on the night. Each change is in <see cref="Adjustments"/>.</summary>
    public required int AdjustmentMinutes { get; set; }

    public TimeSpan? StartsAt { get; set; }

    [MapperIgnore]
    public DbScheduleLane? Lane { get; set; }

    [MapperIgnore]
    public List<DbScheduleItemAdjustment> Adjustments { get; set; } = [];
}
