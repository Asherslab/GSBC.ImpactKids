using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

/// <summary>
/// A computer running ProPresenter with the Impact Kids agent installed, as the admin page sees it.
/// <para>
/// Carries no credential of any kind. The machine's secret exists only as a hash on the server and
/// in the Keychain on the Mac; nothing here can be used to act as the machine.
/// </para>
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ProPresenterMachine
{
    /// <summary>
    /// How long a machine may go without reporting before the admin page calls it offline. The
    /// agent reports every 30 seconds, so this tolerates two missed reports.
    /// </summary>
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(90);

    public required Guid   Id   { get; init; }
    public required string Name { get; init; }

    public required DateTime CreatedAt { get; init; }

    /// <summary>The leader who approved it, by name, for the admin page. Never an id.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>
    /// False between a leader pressing Approve and the agent exchanging its code - normally a
    /// second or two. A machine that stays unenrolled never finished setting up and can be deleted.
    /// </summary>
    public required bool Enrolled { get; init; }

    /// <summary>Where the agent finds ProPresenter's network API. Usually ProPresenter on the same Mac.</summary>
    public required string ProPresenterHost { get; init; }

    public required int ProPresenterPort { get; init; }

    public DateTime? LastSeenAt            { get; init; }
    public string?   AgentVersion          { get; init; }
    public bool?     ProPresenterReachable { get; init; }

    /// <summary>ProPresenter's own description of itself, e.g. "ProPresenter 22".</summary>
    public string? ProPresenterVersion { get; init; }

    /// <summary>The timers set up in ProPresenter, as the agent last reported them - what the admin page offers to choose from.</summary>
    public ImmutableList<ProPresenterTimer> Timers { get; init; } = [];

    /// <summary>The one ProPresenter timer that follows tonight's schedule. Null until a leader picks one.</summary>
    public string? ScheduleTimerUuid { get; init; }

    /// <summary>ProPresenter's libraries, as the agent last reported them - what the admin page offers to copy songs from.</summary>
    public ImmutableList<ProPresenterLibrary> Libraries { get; init; } = [];

    /// <summary>The libraries this machine copies songs from, already resolved by <see cref="ProPresenterLibrary.SongLibraries"/>.</summary>
    public ImmutableList<string> SongLibraryUuids { get; init; } = [];

    [ProtoIgnore]
    public DateTime LocalCreatedAt => CreatedAt.ToLocalTime();

    [ProtoIgnore]
    public DateTime? LocalLastSeenAt => LastSeenAt?.ToLocalTime();

    /// <summary>Computed against the viewer's clock, so the admin page re-renders it rather than trusting a stored flag.</summary>
    public bool IsOnline(DateTime utcNow) =>
        LastSeenAt != null && utcNow - LastSeenAt.Value < OnlineWindow;
}
