using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServiceTypeServices;

public partial class ServiceTypeService
{
    public async Task<BasicResponse?> Delete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbServiceType? serviceType = await db.ServiceTypes
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (serviceType == null)
            return BasicResponse.WithError(ServiceTypeNotFound);

        db.ServiceTypes.Remove(serviceType);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(serviceType.Id, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}