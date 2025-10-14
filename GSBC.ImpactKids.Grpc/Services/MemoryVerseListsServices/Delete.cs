using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.MemoryVerseListsServices;

public partial class MemoryVerseListsService
{
    public async Task<BasicResponse?> Delete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMemoryVerseList? list = await db.MemoryVerseLists
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (list == null)
            return BasicResponse.WithError(MemoryVerseListNotFound);

        db.MemoryVerseLists.Remove(list);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(list.Id, token: token, list.SchoolTermId ?? Guid.Empty);

        return new BasicResponse
        {
            Success = true
        };
    }
}