using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemorisationEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemorisationEntriesServices;

public partial class MemorisationEntriesService
{
    public async Task<BasicResponse> Update(UpdateMemorisationEntryRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMemorisationEntry? memorisationEntry = await db.MemorisationEntries
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (memorisationEntry == null)
            return BasicResponse.WithError(MemorisationEntryNotFound);

        if (request.VerseRecited.IsUpdated)
            memorisationEntry.VerseRecited = request.VerseRecited.Value;

        if (request.FiveDollaryDoosGiven.IsUpdated)
            memorisationEntry.FiveDollaryDoosGiven = request.FiveDollaryDoosGiven.Value;

        if (request.OneDollaryDooGiven.IsUpdated)
            memorisationEntry.OneDollaryDooGiven = request.OneDollaryDooGiven.Value;

        db.MemorisationEntries.Update(memorisationEntry);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}