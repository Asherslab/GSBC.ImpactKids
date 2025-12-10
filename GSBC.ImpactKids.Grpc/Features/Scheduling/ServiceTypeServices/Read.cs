using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServiceTypeServices;

public partial class ServiceTypeService
{
    public async Task<BasicReadResponse<ServiceType>?> Read(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbServiceType? serviceType = await db.ServiceTypes
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (serviceType == null)
            return BasicReadResponse<ServiceType>.WithError(ServiceTypeNotFound);

        return new BasicReadResponse<ServiceType>
        {
            Success = true,
            Entity = converter.Convert(serviceType)
        };
    }
}