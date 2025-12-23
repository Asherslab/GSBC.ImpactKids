using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVersesServices;

public partial class MemoryVersesService
{
    public async Task<BasicReadMultipleResponse<MemoryVerse>?> ReadMultiple(
        MemoryVersesRequest request,
        CallContext         context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbMemoryVerse> query = db.MemoryVerses;

        if (request.IncludeBibleVerses)
            query = query.Include(x => x.BibleVerses);
        
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

        query = query.OrderBy(x => x.Services.OrderBy(y => y.Date).First());
        
        query = query.Paginate(request);

        List<DbMemoryVerse> verses = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<MemoryVerse>
        {
            Success = true,
            Entities = verses.Select(converter.Convert).ToImmutableList()
        };
    }
}