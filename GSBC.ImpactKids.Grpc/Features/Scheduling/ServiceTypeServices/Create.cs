using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services.ServiceTypes;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServiceTypeServices;

public partial class ServiceTypeService
{
    public async Task<BasicResponse?> Create(CreateServiceTypeRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(request.Label))
            return BasicResponse.WithError(ServiceTypeLabelNull);
        
        DbServiceType type = new()
        {
            Id = Guid.Empty,
            Label = request.Label,
            Color = request.Color
        };
        
        await db.ServiceTypes.AddAsync(type, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(type.Id, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}