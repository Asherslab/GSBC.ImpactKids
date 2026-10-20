using GSBC.ImpactKids.Grpc.Data.Models.ProPresenter;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Songs;

/// <summary>
/// A song copied from ProPresenter by a machine's agent. Written only by
/// <c>ProPresenterAgentService.SyncSongs</c>; ProPresenter is the source of truth and nothing here is
/// edited by a leader.
/// <para>
/// <b>Removal is soft.</b> A song deleted in ProPresenter - or in a library a leader stopped copying -
/// gets <see cref="RemovedAt"/> rather than losing its row, so putting it back brings back the same
/// id, and a link to it keeps working.
/// </para>
/// </summary>
public class DbSong
{
    public required Guid Id { get; set; }

    /// <summary>ProPresenter's id for the presentation. Unique: Macs sharing a cloud workspace send the same song with the same id.</summary>
    public required string ProPresenterUuid { get; set; }

    public required string Name    { get; set; }
    public required string Library { get; set; }

    /// <summary>
    /// Groups, arrangements and the default arrangement, as JSON. Replaced whole on every change, the
    /// same as ProPresenter hands it over; nothing queries inside it.
    /// </summary>
    public required string ContentJson { get; set; }

    /// <summary>Base64 SHA-256 of name, library and content - how a sync tells a changed song from one sent again unchanged.</summary>
    public required string ContentHash { get; set; }

    public required DateTimeOffset FirstSyncedAt { get; set; }

    /// <summary>When the content last changed. A sync that finds the song unchanged leaves it alone.</summary>
    public required DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? RemovedAt { get; set; }

    /// <summary>
    /// The machine that last sent it. A sync removes only that machine's songs, so a second Mac copying
    /// other libraries does not delete the first one's.
    /// </summary>
    public Guid? SyncedByMachineId { get; set; }

    [MapperIgnore]
    public DbProPresenterMachine? SyncedByMachine { get; set; }
}
