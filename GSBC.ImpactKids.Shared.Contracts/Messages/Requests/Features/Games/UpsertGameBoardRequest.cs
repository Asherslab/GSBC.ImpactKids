using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpsertGameBoardRequest
{
    public Guid ServiceId { get; set; }

    public int CurrentGame { get; set; }
    public int StepPoints  { get; set; }
    public int BonusPoints { get; set; }

    /// <summary>Display only scaling for the night; see <see cref="GameBoard.PointsMultiplier"/>.</summary>
    public int PointsMultiplier { get; set; }

    /// <summary>Display only scaling for behaviour points, which have their own rate.</summary>
    public int BehaviourPointsMultiplier { get; set; }

    /// <summary>Replaces the stored team list wholesale. Empty means "keep the default four".</summary>
    public ImmutableList<GameTeamDefinition> Teams { get; set; } = [];

    /// <summary>Replaces the stored per game settings wholesale.</summary>
    public ImmutableList<GameDefinition> Games { get; set; } = [];

    public GameDisplayMode DisplayMode { get; set; }

    public bool      Hidden   { get; set; }
    public bool      Paused   { get; set; }
    public DateTime? PausedAt { get; set; }

    /// <summary>Step of the end of night reveal, or null when no reveal is running.</summary>
    public int? RevealStep { get; set; }

    /// <summary>
    /// When the change was made on the device. The server discards anything older
    /// than what it already has, so a queued offline edit cannot undo a newer one.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
