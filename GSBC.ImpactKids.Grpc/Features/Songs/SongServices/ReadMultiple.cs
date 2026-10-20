using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models.Songs;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Songs.SongServices;

public partial class SongService
{
    /// <summary>Every song still in ProPresenter, by name. A song removed there stays in the table but not in this list.</summary>
    public async IAsyncEnumerable<BasicReadMultipleResponse<Song>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbSong> query = db.Songs
            .AsNoTracking()
            .Where(x => x.RemovedAt == null);

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x => x.Name.ToLower().Contains(search.ToLower()));
            }
        }

        query = query.OrderBy(x => x.Name);

        query = query.Paginate(request);

        Dictionary<Guid, (DateTimeOffset Last, int Times)> sung = await SungAsync(token);

        await foreach (BasicReadMultipleResponse<Song> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response with
            {
                // Not a converter of its own: AddConverters registers every IConverter it finds, and one
                // wrapping the song converter would be registered as the song converter.
                Entities = response.Entities.Select(song => sung.TryGetValue(song.Id, out (DateTimeOffset Last, int Times) s)
                        ? song with { LastSungAt = s.Last.UtcDateTime, TimesSung = s.Times }
                        : song)
                    .ToImmutableList()
            };
        }
    }

    /// <summary>
    /// Each song's services up to the end of today: the dates of the services whose schedules have it,
    /// one per service. A song item sits in a lane, or under a section that does.
    /// </summary>
    private async Task<Dictionary<Guid, (DateTimeOffset Last, int Times)>> SungAsync(CancellationToken token)
    {
        (_, DateTimeOffset endOfToday) = ChurchTime.DayBounds(ChurchTime.Today);

        var services = await db.ScheduleItems
            .AsNoTracking()
            .Where(x => x.SongId != null)
            // A CASE over the two ids; EF cannot translate choosing between the two navigations themselves.
            .Select(x => new
            {
                SongId = x.SongId!.Value,
                ScheduleId = x.LaneId != null
                    ? x.Lane!.Block!.ScheduleId
                    : x.ParentItem!.Lane!.Block!.ScheduleId
            })
            .Join(db.Schedules, x => x.ScheduleId, s => s.Id, (x, s) => new { x.SongId, s.ServiceId })
            .Join(db.Services, x => x.ServiceId, s => s.Id, (x, s) => new { x.SongId, ServiceId = s.Id, s.Date })
            .Where(x => x.Date < endOfToday)
            .Distinct()
            .ToListAsync(token);

        return services
            .GroupBy(x => x.SongId)
            .ToDictionary(g => g.Key, g => (g.Max(x => x.Date), g.Count()));
    }
}
