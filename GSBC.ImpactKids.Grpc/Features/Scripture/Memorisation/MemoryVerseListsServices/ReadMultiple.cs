using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVerseListsServices;

public partial class MemoryVerseListsService
{
    public async Task<BasicReadMultipleResponse<MemoryVerseList>?> ReadMultiple(
        MemoryVerseListsRequest request,
        CallContext             context = default
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

        if (request.SchoolTermId != null)
        {
            query = query.Where(x =>
                x.SchoolTermId == request.SchoolTermId
            );
        }

        query = query.OrderBy(x => x.Name);
        
        query = query.Paginate(request);

        List<DbMemoryVerseList> lists = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<MemoryVerseList>
        {
            Success = true,
            Entities = lists.Select(converter.Convert).ToList()
        };
    }
}