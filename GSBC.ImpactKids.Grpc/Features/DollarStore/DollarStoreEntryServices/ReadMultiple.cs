using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.DollarStore.DollarStoreEntryServices;

public partial class DollarStoreEntryService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<DollarStoreEntry>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
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

        query = query.OrderBy(x => x.Service!.Date);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<DollarStoreEntry> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}