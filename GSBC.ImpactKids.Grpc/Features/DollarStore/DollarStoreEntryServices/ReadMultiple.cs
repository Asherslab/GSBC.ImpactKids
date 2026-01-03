using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.DollarStore.DollarStoreEntryServices;

public partial class DollarStoreEntryService
{
    public Task<BasicReadMultipleResponse<DollarStoreEntry>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        return ReadMultiple(new DollarStoreEntriesRequest
        {
            Pagination = request.Pagination,
            SearchString = request.SearchString
        });
    }

    public async Task<BasicReadMultipleResponse<DollarStoreEntry>> ReadMultiple(
        DollarStoreEntriesRequest request,
        CallContext               context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbDollarStoreEntry> query = db.DollarStoreEntries;

        if (request.SearchString != null)
        {
            // ReSharper disable once SpecifyACultureInStringConversionExplicitly
            query = query.Where(x =>
                x.Notes!.ToLower().Contains(request.SearchString.ToLower()) ||
                x.DollarDoosMade.ToString()!.ToLower().Contains(request.SearchString.ToLower())
            );
        }

        if (request.SchoolTermId != null)
        {
            query = query.Where(x =>
                x.Service!.SchoolTermId == request.SchoolTermId
            );
        }

        query = query.OrderBy(x => x.Service!.Date);

        query = query.Paginate(request);

        List<DbDollarStoreEntry> entries = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<DollarStoreEntry>
        {
            Success = true,
            Entities = entries.Select(converter.Convert).ToImmutableList()
        };
    }
}