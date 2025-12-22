using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServicesServices;

public partial class ServicesService
{
    public async Task<BasicReadMultipleResponse<Service>?> ReadMultiple(
        ServicesRequest request,
        CallContext     context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbService> query = db.Services
            .Include(x => x.SchoolTerm)
            .Include(x => x.ServiceType)
            .Include(x => x.DollarStoreEntry);

        if (request.SearchString != null)
        {
            // ReSharper disable once SpecifyACultureInStringConversionExplicitly
            query = query.Where(x =>
                x.Name!.ToLower().Contains(request.SearchString.ToLower()) ||
                x.Date.ToString().ToLower().Contains(request.SearchString.ToLower())
            );
        }

        if (request.Year != null)
        {
            query = query.Where(x =>
                x.Date.Year == request.Year.Value
            );
        }

        if (request.SchoolTermId != null)
        {
            query = query.Where(x =>
                x.SchoolTermId == request.SchoolTermId
            );
        }

        if (request.ServiceTypeId != null)
        {
            query = query.Where(x =>
                x.ServiceTypeId == request.ServiceTypeId
            );
        }

        query = query.OrderBy(x => x.Date);
        
        query = query.Paginate(request);

        List<DbService> terms = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<Service>
        {
            Success = true,
            Entities = terms.Select(converter.Convert).ToList()
        };
    }
}