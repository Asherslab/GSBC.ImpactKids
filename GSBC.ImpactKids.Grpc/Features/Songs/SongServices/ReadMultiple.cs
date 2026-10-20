using GSBC.ImpactKids.Grpc.Data.Models.Songs;
using GSBC.ImpactKids.Grpc.Extensions;
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

        await foreach (BasicReadMultipleResponse<Song> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}
