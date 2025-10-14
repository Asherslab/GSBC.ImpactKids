using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.ServicesServices;

public partial class ServicesService
{
    public async Task<BasicResponse?> Create(CreateServiceRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (request.SchoolTermId == Guid.Empty)
            return BasicResponse.WithError(ServiceSchoolTermNull);
        
        DbSchoolTerm? term = await db.Terms.FirstOrDefaultAsync(x => x.Id == request.SchoolTermId, cancellationToken: token);

        if (term == null)
            return BasicResponse.WithError(SchoolTermNotFound);
        
        if (request.Date == default)
            return BasicResponse.WithError(ServiceDateNull);

        if (string.IsNullOrWhiteSpace(request.Name))
            request.Name = null;
        
        DbService service = new()
        {
            Id = Guid.Empty,
            Name = request.Name,

            Date = request.Date,
            SchoolTermId = request.SchoolTermId
        };
        
        await db.Services.AddAsync(service, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(service.Id, token: token, service.SchoolTermId);

        return new BasicResponse
        {
            Success = true
        };
    }
}