using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.ProPresenter;

/// <summary>
/// A ProPresenter machine: a third kind of caller beside a leader and a wall display, with its own
/// credential, its own token scheme and its own service. See
/// <c>docs/work/2026-10-propresenter-agent.md</c>.
/// <para>
/// <b>Deleting the row is revocation, and it is total.</b> The row holds the only thing that
/// verifies the machine's access tokens (<see cref="TokenSigningKey"/>) and the only thing that
/// checks its secret (<see cref="SecretHash"/>), so once it is gone nothing the machine holds is
/// worth anything.
/// </para>
/// </summary>
public class DbProPresenterMachine
{
    public required Guid   Id   { get; set; }
    public required string Name { get; set; }

    public required DateTimeOffset CreatedAt       { get; set; }
    public          Guid?          CreatedByUserId { get; set; }

    [MapperIgnore]
    public DbUser? CreatedByUser { get; set; }

    /// <summary>
    /// Base64 SHA-256 of the machine secret, null until the agent exchanges its code. A plain digest
    /// is right for the same reason as <c>DbPickupDisplayKey.KeyHash</c>: the secret is 32 random
    /// bytes, so there is no guessable space for a work factor to defend.
    /// </summary>
    public string? SecretHash { get; set; }

    /// <summary>
    /// Signs this machine's access tokens, with the row id as the key id. The secret itself rather
    /// than a digest, because this service both signs and verifies with it. Never leaves the cluster.
    /// </summary>
    public required string TokenSigningKey { get; set; }

    // The pending code, between Approve and the agent's exchange. All four are cleared by the
    // exchange, so a spent code cannot be found again even by its hash.

    public string?         EnrolmentCodeHash   { get; set; }
    public string?         EnrolmentChallenge  { get; set; }
    public string?         EnrolmentRedirectUri { get; set; }
    public DateTimeOffset? EnrolmentExpiresAt  { get; set; }

    public DateTimeOffset? EnrolledAt { get; set; }

    // Configuration, pulled by the agent.

    public required string ProPresenterHost { get; set; }
    public required int    ProPresenterPort { get; set; }

    // Status, pushed by the agent.

    public DateTimeOffset? LastSeenAt            { get; set; }
    public string?         AgentVersion          { get; set; }
    public bool?           ProPresenterReachable { get; set; }
    public string?         ProPresenterVersion   { get; set; }

    /// <summary>The timers set up in ProPresenter, as the agent last reported them - a JSON list of uuid and name.</summary>
    public string? TimersJson { get; set; }

    /// <summary>ProPresenter's libraries, as the agent last reported them - a JSON list of uuid and name.</summary>
    public string? LibrariesJson { get; set; }

    // Leader-owned: which ProPresenter timer follows tonight's schedule.
    public string? ScheduleTimerUuid { get; set; }

    /// <summary>
    /// Leader-owned: the libraries to copy songs from, a JSON list of uuids. <b>Null is not empty</b> -
    /// null means nobody has chosen, which copies the "Songs" library; <c>[]</c> copies nothing. See
    /// <c>ProPresenterLibrary.SongLibraries</c>.
    /// </summary>
    public string? SongLibraryUuidsJson { get; set; }
}
