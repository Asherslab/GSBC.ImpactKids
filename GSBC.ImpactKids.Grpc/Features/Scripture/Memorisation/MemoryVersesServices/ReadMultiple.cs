using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVersesServices;

public partial class MemoryVersesService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<MemoryVerse>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbMemoryVerse> query = db.MemoryVerses
            .Include(x => x.Services)
            .Include(x => x.BibleVerses);

        if (request.SearchString != null)
        {
            query = query.Where(x =>
                x.ReferenceName.ToLower().Contains(request.SearchString.ToLower())
            );
        }

        query = query.OrderBy(x => x.Services.OrderBy(y => y.Date).First());

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<MemoryVerse> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}