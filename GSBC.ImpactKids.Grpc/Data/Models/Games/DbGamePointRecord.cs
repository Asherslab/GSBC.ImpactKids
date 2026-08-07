using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Games;

public class DbGamePointRecord
{
    /// <summary>Client generated - see <see cref="GsbcDbContext.BuildGamesModel"/>.</summary>
    public required Guid Id { get; set; }

    public required GameTeam Team   { get; set; }
    public required int      Points { get; set; }

    /// <summary>Null for behaviour points, which are not scoped to a game.</summary>
    public required int? GameNumber { get; set; }

    public required DateTimeOffset Awarded { get; set; }

    public bool Deleted { get; set; }

    // Relationships \\

    public required Guid ServiceId { get; set; }

    [MapperIgnore]
    public DbService? Service { get; set; }

    public Guid? AwardedUserId { get; set; }

    [MapperIgnore]
    public DbUser? AwardedUser { get; set; }
}
