using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.ServicesServices;

public partial class ServicesService
{
    public async Task<BasicReadResponse<Service>?> ReadByDate(
        ServiceByDateRequest request,
        CallContext          context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbService? service = await db.Services
            .FirstOrDefaultAsync(x => x.Date == request.Date, token);
        
        if (service == null)
            return BasicReadResponse<Service>.WithError(ServiceNotFound);

        return new BasicReadResponse<Service>
        {
            Success = true,
            Entity = converter.Convert(service)
        };
    }
}