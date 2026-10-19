using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;

/// <summary>One side of a split, or the whole of a together block.</summary>
public class DbScheduleLane
{
    public required Guid Id       { get; set; }
    public required Guid BlockId  { get; set; }
    public required int  Position { get; set; }

    /// <summary>The groups this lane covers in a split. Ignored in a block with one lane, which is everybody.</summary>
    public List<Guid> GroupIds { get; set; } = [];

    [MapperIgnore]
    public DbScheduleBlock? Block { get; set; }

    [MapperIgnore]
    public List<DbScheduleItem> Items { get; set; } = [];
}
