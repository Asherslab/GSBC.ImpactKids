using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVerseListsServices;

public partial class MemoryVerseListsService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<MemoryVerseList>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbMemoryVerseList> query = db.MemoryVerseLists;

        if (request.SearchString != null)
        {
            query = query.Where(x =>
                x.Name.ToLower().Contains(request.SearchString.ToLower())
            );
        }

        query = query.OrderBy(x => x.Name);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<MemoryVerseList> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}