using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.DollarStoreEntryServices;

public partial class DollarStoreEntryService
{
    public async Task<BasicReadResponse<DollarStoreEntry>?> Read(
        BasicReadRequest request,
        CallContext      context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbDollarStoreEntry? entry = await db.DollarStoreEntries.FirstOrDefaultAsync(x => x.Id == request.Guid || x.ServiceId == request.Guid, token);

        if (entry == null)
            return BasicReadResponse<DollarStoreEntry>.WithError(DollarStoreEntryNotFound);

        return new BasicReadResponse<DollarStoreEntry>
        {
            Success = true,
            Entity = converter.Convert(entry)
        };
    }
}