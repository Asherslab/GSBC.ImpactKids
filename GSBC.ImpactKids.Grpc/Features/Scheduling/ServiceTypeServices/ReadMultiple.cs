using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServiceTypeServices;

public partial class ServiceTypeService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<ServiceType>>
        BasicReadMultiple(BasicReadMultipleRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbServiceType> query = db.ServiceTypes;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.Label.ToLower().Contains(search.ToLower())
                );
            }
        }

        query = query.OrderBy(x => x.Label);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<ServiceType> response in
                       query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}