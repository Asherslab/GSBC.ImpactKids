using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.ServicesServices;

public partial class ServicesService
{
    public async Task<BasicResponse?> Update(UpdateServiceRequest request, CallContext context = default)
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
            if (request.SchoolTermId.Value == Guid.Empty)
                return BasicResponse.WithError(ServiceSchoolTermNull);
            service.SchoolTermId = request.SchoolTermId.Value;
        }

        db.Services.Update(service);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(service.Id, [$"{nameof(SchoolTerm)}.{service.SchoolTermId}"], token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}