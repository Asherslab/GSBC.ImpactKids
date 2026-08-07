using GSBC.ImpactKids.Grpc.Data.Models.Games;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Games.GameBoardServices;

public partial class GameBoardService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<GameBoard>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbGameBoard> query = db.GameBoards
            .OrderBy(x => x.UpdatedAt);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<GameBoard> response in query.ReturnInBatches(converter,
                           token: token))
        {
            yield return response;
        }
    }
}
