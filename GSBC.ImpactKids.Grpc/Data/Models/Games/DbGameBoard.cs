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

    /// <summary>JSON column - see <see cref="GsbcDbContext.BuildGamesModel"/>.</summary>
    public List<DbGameTeam> Teams { get; set; } = [];

    /// <summary>JSON column. Sparse - only games with a name or combined teams.</summary>
    public List<DbGame> Games { get; set; } = [];

    public required GameDisplayMode DisplayMode { get; set; }

    public required bool            Hidden   { get; set; }
    public required bool            Paused   { get; set; }
    public          DateTimeOffset? PausedAt { get; set; }

    public required DateTimeOffset UpdatedAt { get; set; }

    // Relationships \\

    public required Guid ServiceId { get; set; }

    [MapperIgnore]
    public DbService? Service { get; set; }
}
