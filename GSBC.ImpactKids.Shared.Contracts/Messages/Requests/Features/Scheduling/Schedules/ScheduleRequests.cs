using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;

// One edit each. Removing a group, block, lane or item - and adding a lane to a block - name only
// the one thing, so they take a BasicReadRequest rather than a message of their own.

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateScheduleTemplateRequest
{
    public string? Name { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateScheduleTemplateRequest : IUpdateRequest<Schedule, UpdateScheduleTemplateRequest>
{
    public          Guid    Id                      { get; set; }
    public          string  Name                    { get; set; } = "";
    public          Guid?   DefaultForServiceTypeId { get; set; }

    public static UpdateScheduleTemplateRequest FromEntity(Schedule entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name ?? "",
        DefaultForServiceTypeId = entity.DefaultForServiceTypeId
    };
}

/// <summary>Replaces the service's schedule, whatever it held, with a copy of the template.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ApplyScheduleTemplateRequest
{
    public Guid ServiceId  { get; set; }
    public Guid TemplateId { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class AddScheduleGroupRequest
{
    public Guid   ScheduleId { get; set; }
    public string Name       { get; set; } = "";
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class RenameScheduleGroupRequest
{
    public Guid   GroupId { get; set; }
    public string Name    { get; set; } = "";
}

/// <summary>
/// A split of <see cref="Lanes"/> lanes, placed at <see cref="Position"/> in the main list (see
/// <see cref="ScheduleLayout"/>); at the end when null. Together blocks are never added directly -
/// they are just where the main list's items live.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class AddScheduleBlockRequest
{
    public Guid ScheduleId { get; set; }
    public int? Position   { get; set; }
    public int  Lanes      { get; set; } = 2;
}

/// <summary>A split dragged to <see cref="Position"/> in the main list.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MoveScheduleBlockRequest
{
    public Guid BlockId  { get; set; }
    public int  Position { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SetScheduleLaneGroupsRequest
{
    public Guid       LaneId   { get; set; }
    public List<Guid> GroupIds { get; set; } = [];
}

/// <summary>
/// From an item template when <see cref="ItemTemplateId"/> is set; otherwise a one-off with this name
/// and length. Into a split's lane when <see cref="LaneId"/> is set; otherwise into the main list of
/// <see cref="ScheduleId"/>, where <see cref="Position"/> counts splits as entries.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class AddScheduleItemRequest
{
    public Guid    ScheduleId     { get; set; }
    public Guid?   LaneId         { get; set; }
    public int?    Position       { get; set; }
    public Guid?   ItemTemplateId { get; set; }
    public string? Name           { get; set; }
    public int     Minutes        { get; set; } = 5;
}

/// <summary>
/// <see cref="Minutes"/> is the length the item should run for. Before the night it becomes the plan;
/// once the night is underway the difference is recorded as an adjustment.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateScheduleItemRequest
{
    public Guid      ItemId   { get; set; }
    public string    Name     { get; set; } = "";
    public string?   FillIn   { get; set; }
    public int       Minutes  { get; set; }
    public TimeSpan? StartsAt { get; set; }
}

/// <summary>A drag: into a split's lane, or into the main list when <see cref="LaneId"/> is null.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MoveScheduleItemRequest
{
    public Guid  ItemId   { get; set; }
    public Guid? LaneId   { get; set; }
    public int   Position { get; set; }
}

/// <summary>+5, −1: added to the plan before the night, recorded as an adjustment during it.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class AdjustScheduleItemRequest
{
    public Guid ItemId  { get; set; }
    public int  Minutes { get; set; }
}
