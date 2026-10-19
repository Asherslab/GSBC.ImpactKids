using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;

/// <summary>
/// A running order. Either a template (<see cref="ServiceId"/> null, named) or one service's own
/// schedule. A service's schedule is a copy of a template, never a link to one.
/// </summary>
public class DbSchedule
{
    public required Guid Id { get; set; }

    /// <summary>A template's name. A service's schedule has none; it is named by its service.</summary>
    public string? Name { get; set; }

    public Guid? ServiceId { get; set; }

    [MapperIgnore]
    public DbService? Service { get; set; }

    /// <summary>On a template only: the service type it is the default for. At most one template per type.</summary>
    public Guid? DefaultForServiceTypeId { get; set; }

    [MapperIgnore]
    public DbServiceType? DefaultForServiceType { get; set; }

    [MapperIgnore]
    public List<DbScheduleGroup> Groups { get; set; } = [];

    [MapperIgnore]
    public List<DbScheduleBlock> Blocks { get; set; } = [];
}
