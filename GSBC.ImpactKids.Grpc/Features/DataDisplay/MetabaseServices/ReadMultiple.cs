using GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.DataDisplay.MetabaseServices;

public partial class MetabaseService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<MetabaseEmbed>>
        BasicReadMultiple(BasicReadMultipleRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbMetabaseEmbed> query = db.MetabaseEmbeds;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.Label.ToLower().Contains(search.ToLower())
                );
            }
        }

        query = query.OrderBy(x => x.SortOrder).ThenBy(x => x.Label);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<MetabaseEmbed> response in
                       query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}
