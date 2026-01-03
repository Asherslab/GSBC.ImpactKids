using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.DollarStore.DollarStoreEntryServices;

public partial class DollarStoreEntryService
{
    public async Task<BasicResponse> Create(CreateDollarStoreEntryRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (request.ServiceId == Guid.Empty)
            return BasicResponse.WithError(DollarStoreServiceNull);
        
        DbService? service = await db.Services
            .Include(x => x.DollarStoreEntry)
            .FirstOrDefaultAsync(x => x.Id == request.ServiceId, cancellationToken: token);

        if (service == null)
            return BasicResponse.WithError(ServiceNotFound);
        
        if (service.DollarStoreEntry != null)
            return BasicResponse.WithError(DollarStoreServiceExists);
        
        DbDollarStoreEntry entry = new()
        {
            Id = Guid.Empty,
            ServiceId = service.Id,
            
            DollarDoosMade = request.DollarDoosMade,
            Notes = request.Notes
        };
        
        await db.DollarStoreEntries.AddAsync(entry, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(entry.Id, token: token, service.Id);

        return new BasicResponse
        {
            Success = true
        };
    }
}