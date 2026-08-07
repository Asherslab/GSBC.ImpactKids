using GSBC.ImpactKids.Grpc.Data.Models.Games;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Games.GamePointRecordServices;

public partial class GamePointRecordService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<GamePointRecord>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbGamePointRecord> query = db.GamePointRecords
            .OrderBy(x => x.Awarded);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<GamePointRecord> response in query.ReturnInBatches(converter,
                           token: token))
        {
            yield return response;
        }
    }
}
