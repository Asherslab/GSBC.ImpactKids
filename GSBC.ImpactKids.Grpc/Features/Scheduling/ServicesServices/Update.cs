using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServicesServices;

public partial class ServicesService
{
    public async Task<BasicResponse> Update(UpdateServiceRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbService? service = await db.Services
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (service == null)
            return BasicResponse.WithError(ServiceNotFound);

        if (request.Name.IsUpdated)
        {
            service.Name = request.Name.Value;
            if (string.IsNullOrWhiteSpace(service.Name))
                service.Name = null;
        }

        if (request.Date.IsUpdated)
        {
            if (request.Date.Value == default)
                return BasicResponse.WithError(ServiceDateNull);
            service.Date = request.Date.Value;
        }

        if (request.SchoolTermId.IsUpdated)
        {
            service.SchoolTermId = request.SchoolTermId.Value;
            if (request.SchoolTermId.Value != null)
            {
                DbSchoolTerm? term =
                    await db.Terms.FirstOrDefaultAsync(x => x.Id == request.SchoolTermId.Value,
                        cancellationToken: token);

                if (term == null)
                    return BasicResponse.WithError(SchoolTermNotFound);
            }
        }

        if (request.ServiceTypeId.IsUpdated)
        {
            service.ServiceTypeId = request.ServiceTypeId.Value;
            if (request.ServiceTypeId.Value != null)
            {
                DbServiceType? serviceType =
                    await db.ServiceTypes.FirstOrDefaultAsync(x => x.Id == request.ServiceTypeId.Value,
                        cancellationToken: token);

                if (serviceType == null)
                    return BasicResponse.WithError(ServiceTypeNotFound);
            }
        }

        db.Services.Update(service);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(service.Id, token: token, service.SchoolTermId ?? Guid.Empty);

        return new BasicResponse
        {
            Success = true
        };
    }
}