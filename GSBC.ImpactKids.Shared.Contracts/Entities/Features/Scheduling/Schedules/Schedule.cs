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

    /// <summary>
    /// On the night: the row whose fixed start time Next has reached early. It is shown counting down
    /// to its time, not started - nothing starts by itself, so the next Next starts it.
    /// </summary>
    public Guid? WaitingItemId { get; init; }

    [ProtoIgnore]
    public bool IsTemplate => ServiceId == null;

    /// <summary>Every row a lane holds directly - sections included, the items under them not.</summary>
    [ProtoIgnore]
    public IEnumerable<ScheduleItem> Items => Blocks.SelectMany(b => b.Lanes).SelectMany(l => l.Items);

    /// <summary>Every row, the items under sections included.</summary>
    [ProtoIgnore]
    public IEnumerable<ScheduleItem> AllItems => Items.SelectMany(i => i.Children.Prepend(i));
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

    /// <summary>
    /// The lane the stage timer follows during its split, chosen by a leader. When no lane is chosen,
    /// it follows the one that sets the split's length (see <see cref="ScheduleRun.LeadLane"/>).
    /// </summary>
    public bool OnScreen { get; init; }

    public ImmutableList<ScheduleItem> Items { get; init; } = [];
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ScheduleItem
{
    public required Guid      Id                { get; init; }
    public required string    Name              { get; init; }
    public          string?   FillIn            { get; init; }
    public required bool      NeedsFillIn       { get; init; }

    /// <summary>A placeholder filled in with a song. <see cref="FillIn"/> is then the song's name.</summary>
    public bool IsSong { get; init; }

    /// <summary>The song from the library it was filled in with; null for one typed by name, not in the library yet.</summary>
    public Guid? SongId { get; init; }

    /// <summary>The arrangement of the song to sing. Null for the song's own default.</summary>
    public string? ArrangementUuid { get; init; }

    public          Guid?     ItemTemplateId    { get; init; }
    public required int       PlannedMinutes    { get; init; }
    public required int       AdjustmentMinutes { get; init; }
    public          TimeSpan? StartsAt          { get; init; }

    /// <summary>
    /// A heading ("Songs", "Games") with items under it. The section holds the time - it is what the
    /// timeline and the stage timer use - and <see cref="Children"/> are what was actually done.
    /// </summary>
    public bool IsSection { get; init; }

    /// <summary>The items under a section, in order. Always empty on an item that is not one.</summary>
    public ImmutableList<ScheduleItem> Children { get; init; } = [];

    /// <summary>When it really started on the night, in UTC - set by Next. Null until then.</summary>
    public DateTime? ActualStartedAt { get; init; }

    /// <summary>When it really finished, in UTC. Started and not finished is "on now".</summary>
    public DateTime? ActualEndedAt { get; init; }

    [ProtoIgnore]
    public bool IsRunning => ActualStartedAt != null && ActualEndedAt == null;

    /// <summary>The length it runs for: the plan plus whatever was added or taken away on the night.</summary>
    [ProtoIgnore]
    public int Minutes => Math.Max(0, PlannedMinutes + AdjustmentMinutes);

    /// <summary>"Song 1 — Way Maker" once filled in; "Song 1" until then.</summary>
    [ProtoIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(FillIn) ? Name : $"{Name} — {FillIn}";
}
