using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Games;

public class DbGamePointRecord
{
    /// <summary>Client generated - see <see cref="GsbcDbContext.BuildGamesModel"/>.</summary>
    public required Guid Id { get; set; }

    /// <summary>Positional, matching <see cref="DbGameTeam.Index"/> on the service's board.</summary>
    public required int TeamIndex { get; set; }

    public required int Points { get; set; }

    /// <summary>Null for behaviour points, which are not scoped to a game.</summary>
    public required int? GameNumber { get; set; }

    /// <summary>
    /// Shared by the sibling records written when an alliance is scored, so undo can
    /// take the whole award back, and by every record of one placement round.
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Finishing place for a game scored by placement, 1 based, null for tapped points.
    /// See <see cref="Shared.Contracts.Entities.Features.Games.GamePointRecord.Place"/>.
    /// </summary>
    public int? Place { get; set; }

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
