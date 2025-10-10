using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.ServicesServices;

public partial class ServicesService
{
    public async Task<BasicReadMultipleResponse<Service>?> ReadMultiple(
        ServicesRequest request,
        CallContext     context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbService> query = db.Services;

        if (request.SearchString != null)
        {
            query = query.Where(x => x.Name!.ToLower().Contains(request.SearchString.ToLower()));
        }

        if (request.SchoolTermId != null)
        {
            query = query.Where(x =>
                x.SchoolTermId == request.SchoolTermId 
            );
        }

        query = query.OrderBy(x => x.Date);

        List<DbService> terms = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<Service>
        {
            Success = true,
            Entities = terms.Select(converter.Convert).ToList()
        };
    }
}