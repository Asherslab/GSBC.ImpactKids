using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

/// <summary>
/// A running order, whole: its groups, and its blocks of lanes of items, each list in order.
/// Either a template (no <see cref="ServiceId"/>, named) or one service's own schedule, copied from
/// a template when it was applied. See docs/work/2026-10-service-schedules.md for the shape.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record Schedule : IIdentifiable
{
    public required Guid    Id   { get; init; }
    public          string? Name { get; init; }

    public Guid? ServiceId               { get; init; }
    public Guid? DefaultForServiceTypeId { get; init; }

    public ImmutableList<ScheduleGroup> Groups { get; init; } = [];
    public ImmutableList<ScheduleBlock> Blocks { get; init; } = [];

    [ProtoIgnore]
    public bool IsTemplate => ServiceId == null;

    [ProtoIgnore]
    public IEnumerable<ScheduleItem> Items => Blocks.SelectMany(b => b.Lanes).SelectMany(l => l.Items);
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ScheduleGroup
{
    public required Guid   Id   { get; init; }
    public required string Name { get; init; }
}

/// <summary>A stretch of the night. One lane is everybody together; two or more is a split.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ScheduleBlock
{
    public required Guid Id { get; init; }

    public ImmutableList<ScheduleLane> Lanes { get; init; } = [];

    [ProtoIgnore]
    public bool IsSplit => Lanes.Count > 1;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ScheduleLane
{
    public required Guid Id { get; init; }

    /// <summary>The groups this lane covers in a split. Meaningless in a together block.</summary>
    public ImmutableList<Guid> GroupIds { get; init; } = [];

    public ImmutableList<ScheduleItem> Items { get; init; } = [];
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ScheduleItem
{
    public required Guid      Id                { get; init; }
    public required string    Name              { get; init; }
    public          string?   FillIn            { get; init; }
    public required bool      NeedsFillIn       { get; init; }
    public          Guid?     ItemTemplateId    { get; init; }
    public required int       PlannedMinutes    { get; init; }
    public required int       AdjustmentMinutes { get; init; }
    public          TimeSpan? StartsAt          { get; init; }

    /// <summary>The length it runs for: the plan plus whatever was added or taken away on the night.</summary>
    [ProtoIgnore]
    public int Minutes => Math.Max(0, PlannedMinutes + AdjustmentMinutes);

    /// <summary>"Song 1 — Way Maker" once filled in; "Song 1" until then.</summary>
    [ProtoIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(FillIn) ? Name : $"{Name} — {FillIn}";
}
