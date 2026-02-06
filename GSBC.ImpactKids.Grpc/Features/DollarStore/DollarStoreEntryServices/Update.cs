using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.DollarStore.DollarStoreEntryServices;

public partial class DollarStoreEntryService
{
    public async Task<BasicResponse> Update(UpdateDollarStoreEntryRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbDollarStoreEntry? entry = await db.DollarStoreEntries
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (entry == null)
            return BasicResponse.WithError(DollarStoreEntryNotFound);
        
        if (request.DollarDoosMade.IsUpdated)
        {
            entry.DollarDoosMade = request.DollarDoosMade.Value;
        }

        if (request.Notes.IsUpdated)
        {
            entry.Notes = request.Notes.Value;
            if (string.IsNullOrWhiteSpace(entry.Notes))
                entry.Notes = null;
        }
        
        db.DollarStoreEntries.Update(entry);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}