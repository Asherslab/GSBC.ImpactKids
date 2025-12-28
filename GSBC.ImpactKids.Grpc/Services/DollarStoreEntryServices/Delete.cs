using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.DollarStoreEntryServices;

public partial class DollarStoreEntryService
{
    public async Task<BasicResponse> Delete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbDollarStoreEntry? entry = await db.DollarStoreEntries
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (entry == null)
            return BasicResponse.WithError(DollarStoreEntryNotFound);

        Guid serviceId = entry.ServiceId;
        db.DollarStoreEntries.Remove(entry);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(entry.Id, token: token, serviceId);

        return new BasicResponse
        {
            Success = true
        };
    }
}