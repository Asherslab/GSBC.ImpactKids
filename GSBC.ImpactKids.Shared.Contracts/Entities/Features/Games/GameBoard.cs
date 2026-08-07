namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

/// <summary>
/// Shared scoreboard settings for one service - which game is running, how the
/// buttons are configured, and what the public display is allowed to show.
/// One per service.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record GameBoard : IIdentifiable
{
    public required Guid Id        { get; init; }
    public required Guid ServiceId { get; init; }

    /// <summary>1 based. Bumped by "new game", which starts a fresh split.</summary>
    public required int CurrentGame { get; init; }

    /// <summary>2 or 4.</summary>
    public required int TeamCount { get; init; }

    /// <summary>Points awarded by the main tap and removed by the minus button.</summary>
    public required int StepPoints { get; init; }

    /// <summary>Points awarded by the secondary bonus button.</summary>
    public required int BonusPoints { get; init; }

    /// <summary>Whether the wall display shows the night's totals or just this game.</summary>
    public required GameDisplayMode DisplayMode { get; init; }

    /// <summary>Public display shows a holding screen instead of the scores.</summary>
    public required bool Hidden { get; init; }

    /// <summary>
    /// Public display is frozen. Scoring carries on as normal - the display just
    /// ignores anything awarded after <see cref="PausedAt"/> until it resumes.
    /// </summary>
    public required bool Paused { get; init; }

    public required DateTime? PausedAt { get; init; }

    /// <summary>
    /// Last write wins, so a board edit queued offline can never clobber a newer one.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    public static GameBoard Default(Guid serviceId) => new()
    {
        Id = Guid.Empty,
        ServiceId = serviceId,
        CurrentGame = 1,
        TeamCount = 4,
        StepPoints = 1,
        BonusPoints = 5,
        DisplayMode = GameDisplayMode.Totals,
        Hidden = false,
        Paused = false,
        PausedAt = null,
        UpdatedAt = DateTime.UnixEpoch
    };
}
