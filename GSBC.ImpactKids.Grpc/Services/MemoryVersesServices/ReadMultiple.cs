using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.MemoryVersesServices;

public partial class MemoryVersesService
{
    public async Task<BasicReadMultipleResponse<MemoryVerse>> ReadMultiple(
        MemoryVersesRequest request,
        CallContext         context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbMemoryVerse> query = db.MemoryVerses;

        if (request.SearchString != null)
        {
            query = query.Where(x =>
                x.ReferenceName.ToLower().Contains(request.SearchString.ToLower())
            );
        }

        if (request.ServiceId != null)
        {
            query = query.Where(x =>
                x.Services.Any(y => y.Id == request.ServiceId)
            );
        }

        if (request.MemoryVerseListId != null)
        {
            query = query.Where(x =>
                x.MemoryVerseListId == request.MemoryVerseListId
            );
        }

        query = query.OrderBy(x => x.Services.Count);

        List<DbMemoryVerse> verses = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<MemoryVerse>
        {
            Success = true,
            Entities = verses.Select(converter.Convert).ToList()
        };
    }
}