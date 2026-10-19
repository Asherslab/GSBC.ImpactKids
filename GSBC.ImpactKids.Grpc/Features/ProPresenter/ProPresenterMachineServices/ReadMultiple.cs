using GSBC.ImpactKids.Grpc.Data.Models.ProPresenter;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.ProPresenterMachineServices;

public partial class ProPresenterMachineService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<ProPresenterMachine>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbProPresenterMachine> query = db.ProPresenterMachines
            .Include(x => x.CreatedByUser)
            .OrderBy(x => x.Name);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<ProPresenterMachine> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}
