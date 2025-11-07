using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemorisationEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemorisationEntriesServices;

public partial class MemorisationEntriesService
{
    public async Task<BasicResponse?> Update(UpdateMemorisationEntryRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMemorisationEntry? memorisationEntry = await db.MemorisationEntries
            .FirstOrDefaultAsync(x =>
                    x.PersonId == request.PersonId &&
                    x.ServiceId == request.ServiceId &&
                    x.MemoryVerseId == request.MemoryVerseId,
                token
            );

        if (memorisationEntry == null)
        {
            memorisationEntry = new DbMemorisationEntry
            {
                PersonId = request.PersonId,
                ServiceId = request.ServiceId,
                MemoryVerseId = request.MemoryVerseId
            };

            try
            {
                await db.MemorisationEntries.AddAsync(memorisationEntry, token);
                await db.SaveChangesAsync(token);
            }
            catch (Exception)
            {
                return BasicResponse.WithError(MemorisationEntryNotFound);
            }
        }

        if (request.VerseRecited.IsUpdated)
            memorisationEntry.VerseRecited = request.VerseRecited.Value;

        if (request.FiveDollaryDoosGiven.IsUpdated)
            memorisationEntry.FiveDollaryDoosGiven = request.FiveDollaryDoosGiven.Value;

        if (request.OneDollaryDooGiven.IsUpdated)
            memorisationEntry.OneDollaryDooGiven = request.OneDollaryDooGiven.Value;

        db.MemorisationEntries.Update(memorisationEntry);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(memorisationEntry.PersonId, token: token, memorisationEntry.ServiceId, memorisationEntry.MemoryVerseId);

        return new BasicResponse
        {
            Success = true
        };
    }
}