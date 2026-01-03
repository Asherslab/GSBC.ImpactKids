using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services.ServiceTypes;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServiceTypeServices;

public partial class ServiceTypeService
{
    public async Task<BasicResponse> Update(UpdateServiceTypeRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbServiceType? serviceType = await db.ServiceTypes
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (serviceType == null)
            return BasicResponse.WithError(ServiceTypeNotFound);

        if (request.Label.IsUpdated)
        {
            if (string.IsNullOrWhiteSpace(serviceType.Label))
                return BasicResponse.WithError(ServiceTypeLabelNull);
            serviceType.Label = request.Label.Value;
        }

        if (request.Color.IsUpdated)
        {
            serviceType.Color = request.Color.Value;
            if (string.IsNullOrWhiteSpace(serviceType.Color))
                serviceType.Color = null;
        }

        db.ServiceTypes.Update(serviceType);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(serviceType.Id, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}