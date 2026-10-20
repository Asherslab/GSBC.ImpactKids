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
    ProPresenterApi   proPresenter,
    ImpactKidsClient  impactKids,
    AgentState        state,
    ILogger<SongSync> logger
)
{
    /// <summary>How often ProPresenter is read. A change of chosen libraries is picked up at once instead.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan ResendAfter = TimeSpan.FromHours(1);

    private DateTimeOffset _lastRead = DateTimeOffset.MinValue;
    private string?        _lastLibraries;
    private string?        _sentHash;
    private DateTimeOffset _sentAt = DateTimeOffset.MinValue;

    /// <param name="libraries">ProPresenter's libraries as this tick read them; null when ProPresenter did not answer.</param>
    public async Task SyncIfDueAsync(List<ProPresenterApi.LibraryId>? libraries, CancellationToken token)
    {
        if (state.Configuration is not { } configuration || libraries == null)
            return;

        DateTimeOffset now    = DateTimeOffset.UtcNow;
        string         chosen = string.Join(",", configuration.SongLibraryUuids);

        if (chosen == _lastLibraries && now - _lastRead < Interval)
            return;

        // Not marked as read: an incomplete read is tried again on the next tick, not in five minutes.
        List<ProPresenterSong>? songs = await ReadAsync(libraries, configuration.SongLibraryUuids, token);

        if (songs == null)
            return;

        _lastRead = now;
        _lastLibraries = chosen;

        string hash = Hash(songs);

        if (hash == _sentHash && now - _sentAt < ResendAfter)
            return;

        BasicResponse response = await impactKids.Agent.SyncSongs(new SyncProPresenterSongsRequest { Songs = songs }, token);

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
