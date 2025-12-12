using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServicesServices;

public partial class ServicesService
{
    public async Task<BasicReadResponse<Service>?> Read(ServiceRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbService> query = db.Services
            .Include(x => x.SchoolTerm)
            .Include(x => x.ServiceType);
        DbService? service;

        if (request.PreviousService)
        {
            service = await query
                .OrderByDescending(x => x.Date)
                .FirstOrDefaultAsync(x => x.Date <= DateTime.Now.Date, token);
        }
        else if (request.UpcomingService)
        {
            service = await query
                .OrderBy(x => x.Date)
                .FirstOrDefaultAsync(x => x.Date >= DateTime.Now.Date, token);
        }
        else
        {
            service = await query.FirstOrDefaultAsync(x => x.Id == request.Guid, token);
        }

        if (service == null)
            return BasicReadResponse<Service>.WithError(ServiceNotFound);

        return new BasicReadResponse<Service>
        {
            Success = true,
            Entity = converter.Convert(service)
        };
    }
}