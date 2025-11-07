using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVersesServices;

public partial class MemoryVersesService
{
    public async Task<BasicResponse?> Delete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMemoryVerse? verse = await db.MemoryVerses
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (verse == null)
            return BasicResponse.WithError(MemoryVerseNotFound);

        db.MemoryVerses.Remove(verse);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(verse.Id, token: token, verse.MemoryVerseListId);

        return new BasicResponse
        {
            Success = true
        };
    }
}