using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.ServicesServices;

public partial class ServicesService
{
    public async Task<BasicReadResponse<Service>?> Read(ServiceRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbService? service;

        if (request.PreviousService)
        {
            service = await db.Services
                .OrderByDescending(x => x.Date)
                .FirstOrDefaultAsync(x => x.Date <= DateTime.Now.Date, token);
        }
        else if (request.UpcomingService)
        {
            service = await db.Services
                .OrderBy(x => x.Date)
                .FirstOrDefaultAsync(x => x.Date >= DateTime.Now.Date, token);
        }
        else
        {
            service = await db.Services.FirstOrDefaultAsync(x => x.Id == request.Guid, token);
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