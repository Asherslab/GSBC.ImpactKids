using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models.Songs;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.ProPresenterAgentServices;

public partial class ProPresenterAgentService
{
    /// <summary>
    /// Bounds on what one machine may send, because the agent is a caller we do not fully trust to be
    /// sensible. The Impact Kids library is 26 songs; these leave room for a church's whole catalogue.
    /// </summary>
    private const int MaxSongs           = 5000;
    private const int MaxGroupsPerSong   = 100;
    private const int MaxSlidesPerGroup  = 100;
    private const int MaxSlideLength     = 2000;
    private const int MaxArrangements    = 50;
    private const int MaxArrangementSize = 200;

    /// <summary>
    /// The machine's whole song list: new songs added, changed ones rewritten, and this machine's songs
    /// that are missing marked removed. A song sent again unchanged is not touched, so
    /// <see cref="DbSong.UpdatedAt"/> means the words changed, not that the agent looked.
    /// <para>
    /// <b>The change event is sent only when a song changed.</b> The agent syncs every few minutes, and
    /// an event per sync would have every leader's phone re-read the library for nothing.
    /// </para>
    /// </summary>
    public async Task<BasicResponse> SyncSongs(
        SyncProPresenterSongsRequest request,
        CallContext                  context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        Guid? machineId = CallingMachine(context);

        if (machineId == null)
            return BasicResponse.WithError(PermissionDenied);

        if (!await db.ProPresenterMachines.AnyAsync(x => x.Id == machineId, token))
            return BasicResponse.WithError(ProPresenterMachineNotFound);

        // Bounded, and one per uuid - a song in two chosen libraries arrives twice.
        List<ProPresenterSong> incoming = request.Songs
            .Where(x => !string.IsNullOrWhiteSpace(x.Uuid) && !string.IsNullOrWhiteSpace(x.Name))
            .DistinctBy(x => x.Uuid)
            .Take(MaxSongs)
            .ToList();

        HashSet<string> incomingUuids = incoming.Select(x => Truncate(x.Uuid, 64)!).ToHashSet();

        Dictionary<string, DbSong> existing = await db.Songs
            .Where(x => incomingUuids.Contains(x.ProPresenterUuid) || x.SyncedByMachineId == machineId)
            .ToDictionaryAsync(x => x.ProPresenterUuid, token);

        DateTimeOffset now     = DateTimeOffset.UtcNow;
        bool           changed = false;

        foreach (ProPresenterSong song in incoming)
        {
            string uuid    = Truncate(song.Uuid, 64)!;
            string name    = Truncate(song.Name.Trim(), 200)!;
            string library = Truncate(song.Library.Trim(), 200)!;
            string json    = Bounded(song).ToJson();
            string hash    = SongContent.Hash(name, library, json);

            if (!existing.TryGetValue(uuid, out DbSong? row))
            {
                db.Songs.Add(new DbSong
                {
                    Id = Guid.NewGuid(),
                    ProPresenterUuid = uuid,
                    Name = name,
                    Library = library,
                    ContentJson = json,
                    ContentHash = hash,
                    FirstSyncedAt = now,
                    UpdatedAt = now,
                    SyncedByMachineId = machineId
                });

                changed = true;
                continue;
            }

            // Tracked, so only the properties assigned a different value are written.
            row.SyncedByMachineId = machineId;

            if (row.RemovedAt != null)
            {
                row.RemovedAt = null;
                changed = true;
            }

            if (row.ContentHash != hash)
            {
                row.Name = name;
                row.Library = library;
                row.ContentJson = json;
                row.ContentHash = hash;
                row.UpdatedAt = now;
                changed = true;
            }
        }

        // Only this machine's: another Mac copying other libraries keeps its songs.
        foreach (DbSong gone in existing.Values.Where(x =>
                     x.SyncedByMachineId == machineId && x.RemovedAt == null && !incomingUuids.Contains(x.ProPresenterUuid)))
        {
            gone.RemovedAt = now;
            changed = true;
        }

        await db.SaveChangesAsync(token);

        if (changed)
            await songEventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }

    private static SongContent Bounded(ProPresenterSong song) => new(
        song.Groups
            .Take(MaxGroupsPerSong)
            .Select(g => g with
            {
                Uuid = Truncate(g.Uuid, 64)!,
                Name = Truncate(g.Name, 200)!,
                Color = Truncate(g.Color, 16),
                Slides = g.Slides
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Take(MaxSlidesPerGroup)
                    .Select(s => Truncate(s, MaxSlideLength)!)
                    .ToImmutableList()
            })
            .ToImmutableList(),
        song.Arrangements
            .Take(MaxArrangements)
            .Select(a => a with
            {
                Uuid = Truncate(a.Uuid, 64)!,
                Name = Truncate(a.Name, 200)!,
                GroupUuids = a.GroupUuids.Take(MaxArrangementSize).Select(u => Truncate(u, 64)!).ToImmutableList()
            })
            .ToImmutableList(),
        Truncate(song.DefaultArrangementUuid, 64));
}
