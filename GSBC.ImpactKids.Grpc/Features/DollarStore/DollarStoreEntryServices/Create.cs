using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DollarStore;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.DollarStore.DollarStoreEntryServices;

public partial class DollarStoreEntryService
{
    public async Task<BasicReadResponse<Guid?>> Create(CreateDollarStoreEntryRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (request.ServiceId == Guid.Empty)
            return BasicReadResponse<Guid?>.WithError(DollarStoreServiceNull);
        
        DbService? service = await db.Services
            .Include(x => x.DollarStoreEntry)
            .FirstOrDefaultAsync(x => x.Id == request.ServiceId, cancellationToken: token);

        if (service == null)
            return BasicReadResponse<Guid?>.WithError(ServiceNotFound);
        
        if (service.DollarStoreEntry != null)
            return BasicReadResponse<Guid?>.WithError(DollarStoreServiceExists);
        
        DbDollarStoreEntry entry = new()
        {
            Id = Guid.Empty,
            ServiceId = service.Id,
            
            DollarDoosMade = request.DollarDoosMade,
            Notes = request.Notes
        };
        
        await db.DollarStoreEntries.AddAsync(entry, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = entry.Id,
            Success = true
        };
    }
}