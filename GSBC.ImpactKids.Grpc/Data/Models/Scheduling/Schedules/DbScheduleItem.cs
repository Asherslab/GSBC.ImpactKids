using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;

/// <summary>
/// One row of a schedule. Either it belongs to a lane (<see cref="LaneId"/>), or it sits under a
/// section (<see cref="ParentItemId"/>) - never both. A section ("Songs", "Games") holds the time for
/// the stage timer; the items under it ("Way Maker", "Dodgeball") are what was actually done, and get
/// their own recorded times later.
/// </summary>
public class DbScheduleItem
{
    public required Guid   Id       { get; set; }
    public          Guid?  LaneId   { get; set; }
    public required int    Position { get; set; }
    public required string Name     { get; set; }

    /// <summary>A heading with items under it. Its length is the section's; the items under it have none that counts.</summary>
    public bool IsSection { get; set; }

    /// <summary>The section this item sits under. One level only: a section is never under another.</summary>
    public Guid? ParentItemId { get; set; }

    [MapperIgnore]
    public DbScheduleItem? ParentItem { get; set; }

    [MapperIgnore]
    public List<DbScheduleItem> Children { get; set; } = [];

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

    /// <summary>When Next really started it on the night. With <see cref="ActualEndedAt"/>, the time it actually took.</summary>
    public DateTimeOffset? ActualStartedAt { get; set; }

    public DateTimeOffset? ActualEndedAt { get; set; }

    [MapperIgnore]
    public DbScheduleLane? Lane { get; set; }

    [MapperIgnore]
    public List<DbScheduleItemAdjustment> Adjustments { get; set; } = [];
}
