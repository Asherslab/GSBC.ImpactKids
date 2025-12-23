using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServiceTypeServices;

public partial class ServiceTypeService
{
    public async Task<BasicReadMultipleResponse<ServiceType>?>
        ReadMultiple(BasicReadMultipleRequest request, CallContext context = default)
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

        List<DbServiceType> types = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<ServiceType>
        {
            Success = true,
            Entities = types.Select(converter.Convert).ToImmutableList()
        };
    }
}