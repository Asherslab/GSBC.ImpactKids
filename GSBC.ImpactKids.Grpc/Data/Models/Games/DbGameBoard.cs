using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Games;

public class DbGameBoard
{
    public required Guid Id { get; set; }

    public required int CurrentGame { get; set; }
    public required int StepPoints  { get; set; }
    public required int BonusPoints { get; set; }

    /// <summary>
    /// Display only scaling for the night. Defaulted rather than required so a board
    /// written before the column existed reads back as an ordinary 1000x night. See
    /// <see cref="Shared.Contracts.Entities.Features.Games.GameBoard.PointsMultiplier"/>.
    /// </summary>
    public int PointsMultiplier { get; set; } = GameMultipliers.Default;

    /// <summary>
    /// Display only scaling for behaviour points. Its own column rather than the night's
    /// value, because behaviour points are priced separately to games.
    /// </summary>
    public int BehaviourPointsMultiplier { get; set; } = GameMultipliers.Default;

    /// <summary>JSON column - see <see cref="GsbcDbContext.BuildGamesModel"/>.</summary>
    public List<DbGameTeam> Teams { get; set; } = [];

    /// <summary>JSON column. Sparse - only games with a name or combined teams.</summary>
    public List<DbGame> Games { get; set; } = [];

    public required GameDisplayMode DisplayMode { get; set; }

    public required bool            Hidden   { get; set; }
    public required bool            Paused   { get; set; }
    public          DateTimeOffset? PausedAt { get; set; }

    /// <summary>Step of the end of night reveal, or null when no reveal is running.</summary>
    public int? RevealStep { get; set; }

    public required DateTimeOffset UpdatedAt { get; set; }

    // Relationships \\

    public required Guid ServiceId { get; set; }

    [MapperIgnore]
    public DbService? Service { get; set; }
}
