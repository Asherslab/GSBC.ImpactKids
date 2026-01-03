using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServicesServices;

public partial class ServicesService
{
    public async Task<BasicResponse> Create(CreateServiceRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (request.SchoolTermId != null)
        {
            DbSchoolTerm? term =
                await db.Terms.FirstOrDefaultAsync(x => x.Id == request.SchoolTermId, cancellationToken: token);

            if (term == null)
                return BasicResponse.WithError(SchoolTermNotFound);
        }

        if (request.ServiceTypeId != null)
        {
            DbServiceType? serviceType =
                await db.ServiceTypes.FirstOrDefaultAsync(x => x.Id == request.ServiceTypeId, cancellationToken: token);

            if (serviceType == null)
                return BasicResponse.WithError(ServiceTypeNotFound);
        }

        if (request.Date == default)
            return BasicResponse.WithError(ServiceDateNull);

        if (string.IsNullOrWhiteSpace(request.Name))
            request.Name = null;

        DbService service = new()
        {
            Id = Guid.Empty,
            Name = request.Name,

            Date = request.Date,
            SchoolTermId = request.SchoolTermId,
            ServiceTypeId = request.ServiceTypeId
        };

        await db.Services.AddAsync(service, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(service.Id, token: token, service.SchoolTermId ?? Guid.Empty);

        return new BasicResponse
        {
            Success = true
        };
    }
}