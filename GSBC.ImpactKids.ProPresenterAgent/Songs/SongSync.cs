using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using GSBC.ImpactKids.ProPresenterAgent.ImpactKids;
using GSBC.ImpactKids.ProPresenterAgent.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.ProPresenterAgent.Songs;

/// <summary>
/// Copies the songs in the machine's chosen libraries into Impact Kids, for the song library page.
/// Reads ProPresenter's network API - not its workspace files, which are CRDT documents in a format
/// ProPresenter 22 does not document and is still changing. The API sees only the active workspace.
/// <para>
/// <b>So it reads only while the machine's chosen workspace is open</b>, checked before and after the
/// read so a switch halfway through is not sent. Another workspace's libraries are not this one's
/// with songs missing - and its "Songs" library even has the same uuid. See
/// <see cref="ProPresenterWorkspaces"/>.
/// </para>
/// <para>
/// <b>It sends only a complete list.</b> The server takes a song this machine sent before and has not
/// sent now as deleted in ProPresenter, so a read cut short - ProPresenter closed halfway, one
/// presentation timing out - must send nothing at all rather than the songs it did get.
/// </para>
/// <para>
/// Reading is cheap and local; sending is not, so a read whose songs hash the same as the last one
/// sent goes no further. It is sent again anyway after <see cref="ResendAfter"/>, so a server that lost
/// its copy - a restored database - is put right without restarting the Mac.
/// </para>
/// </summary>
public sealed class SongSync(
    ProPresenterApi        proPresenter,
    ProPresenterWorkspaces workspaces,
    ImpactKidsClient       impactKids,
    AgentState             state,
    ILogger<SongSync>      logger
)
{
    /// <summary>How often ProPresenter is read. A change of chosen libraries or workspace is picked up at once instead.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan ResendAfter = TimeSpan.FromHours(1);

    private DateTimeOffset _lastRead = DateTimeOffset.MinValue;
    private string?        _lastChoice;
    private string?        _sentHash;
    private DateTimeOffset _sentAt = DateTimeOffset.MinValue;
    private string?        _skipped;

    /// <param name="libraries">ProPresenter's libraries as this tick read them; null when ProPresenter did not answer.</param>
    /// <param name="activeWorkspace">The workspace open in ProPresenter as this tick read it; null when it could not be told.</param>
    public async Task SyncIfDueAsync(List<ProPresenterApi.LibraryId>? libraries, string? activeWorkspace, CancellationToken token)
    {
        if (state.Configuration is not { } configuration || libraries == null)
            return;

        if (Skip(configuration.SongWorkspaceUuid, activeWorkspace) is { } reason)
        {
            // Once per reason, not every 30 seconds for a night spent in the church's workspace.
            if (reason != _skipped)
                logger.LogInformation("Not copying songs: {Reason}", reason);

            _skipped = reason;
            return;
        }

        _skipped = null;

        DateTimeOffset now    = DateTimeOffset.UtcNow;
        string         choice = $"{configuration.SongWorkspaceUuid}:{string.Join(",", configuration.SongLibraryUuids)}";

        // The choice includes the workspace, so coming back to it is read at once.
        if (choice == _lastChoice && now - _lastRead < Interval)
            return;

        // Not marked as read: an incomplete read is tried again on the next tick, not in five minutes.
        List<ProPresenterSong>? songs = await ReadAsync(libraries, configuration.SongLibraryUuids, token);

        if (songs == null)
            return;

        // Switched while reading: some of these songs may be the other workspace's. Not sent, and
        // not marked as read, so the next tick decides afresh.
        if (workspaces.Read()?.ActiveUuid != configuration.SongWorkspaceUuid)
        {
            logger.LogInformation("ProPresenter changed workspace while its songs were read; not sending them");
            return;
        }

        _lastRead = now;
        _lastChoice = choice;

        string hash = Hash(songs);

        if (hash == _sentHash && now - _sentAt < ResendAfter)
            return;

        BasicResponse response = await impactKids.Agent.SyncSongs(new SyncProPresenterSongsRequest
        {
            WorkspaceUuid = configuration.SongWorkspaceUuid,
            Songs = songs
        }, token);

        if (!response.Success)
        {
            logger.LogWarning("Impact Kids refused the songs: {Error}", response.Error);
            return;
        }

        _sentHash = hash;
        _sentAt = now;

        logger.LogInformation("Sent {Count} songs from {Libraries}", songs.Count,
            string.Join(", ", libraries.Where(l => configuration.SongLibraryUuids.Contains(l.Uuid)).Select(l => l.Name)));
    }

    /// <summary>Why songs are not read this tick, or null when the chosen workspace is the one open.</summary>
    private static string? Skip(string? chosen, string? active) =>
        chosen == null ? "no ProPresenter workspace has been chosen for songs on the admin page"
        : active == null ? "cannot tell which workspace ProPresenter has open"
        : active != chosen ? "ProPresenter has a different workspace open from the one chosen for songs"
        : null;

    /// <summary>Every song in the chosen libraries, or null if any part could not be read.</summary>
    private async Task<List<ProPresenterSong>?> ReadAsync(
        List<ProPresenterApi.LibraryId> libraries,
        List<string>                    chosen,
        CancellationToken               token
    )
    {
        List<ProPresenterSong> songs = [];

        foreach (string libraryUuid in chosen)
        {
            ProPresenterApi.LibraryId? library = libraries.FirstOrDefault(x => x.Uuid == libraryUuid);

            // Deleted or renamed away in ProPresenter. Its songs are no longer there either, so leaving
            // them out - and letting the server remove them - is the truth, not a failed read.
            if (library == null)
            {
                logger.LogInformation("Song library {Uuid} is no longer in ProPresenter; its songs will be removed", libraryUuid);
                continue;
            }

            List<ProPresenterApi.LibraryItem>? items = await proPresenter.GetLibraryItemsAsync(library.Uuid, token);

            if (items == null)
                return null;

            foreach (ProPresenterApi.LibraryItem item in items)
            {
                ProPresenterApi.Presentation? presentation = await proPresenter.GetPresentationAsync(item.Uuid, token);

                if (presentation == null)
                    return null;

                songs.Add(ToSong(presentation, library.Name));
            }
        }

        return songs;
    }

    private static ProPresenterSong ToSong(ProPresenterApi.Presentation presentation, string library)
    {
        string uuid = presentation.Id.Uuid;

        return new ProPresenterSong
        {
            Uuid = uuid,
            Name = presentation.Id.Name,
            Library = library,
            Groups = (presentation.Groups ?? [])
                .Select(g => new SongGroup
                {
                    Uuid = g.Uuid,
                    Name = g.Name ?? "",
                    Color = Hex(g.Color),
                    Slides = (g.Slides ?? [])
                        .Select(s => s.Text?.Trim() ?? "")
                        .Where(s => s.Length > 0)
                        .ToImmutableList()
                })
                .ToList(),
            // "Master" is ProPresenter's made-up "every group once", carrying the presentation's own
            // uuid. The page shows that as the song's groups; it is not an arrangement anybody made.
            Arrangements = (presentation.Arrangements ?? [])
                .Where(a => a.Id.Uuid != uuid)
                .Select(a => new SongArrangement
                {
                    Uuid = a.Id.Uuid,
                    Name = a.Id.Name,
                    GroupUuids = (a.Groups ?? []).ToImmutableList()
                })
                .ToList(),
            DefaultArrangementUuid = presentation.CurrentArrangement == uuid ? null : presentation.CurrentArrangement
        };
    }

    private static string? Hex(ProPresenterApi.GroupColor? color) =>
        color == null ? null : $"#{Channel(color.Red):X2}{Channel(color.Green):X2}{Channel(color.Blue):X2}";

    private static int Channel(double value) => (int)Math.Round(Math.Clamp(value, 0, 1) * 255);

    private static string Hash(List<ProPresenterSong> songs) =>
        Convert.ToBase64String(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(songs)));
}
