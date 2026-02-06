using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVersesServices;

public partial class MemoryVersesService
{
    public async Task<BasicResponse> CreateRelationship(BasicMultipleRelationshipRequest<MemoryVerse, BibleVerse> request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMemoryVerseBibleVerseRelationship? rel = await db.Set<DbMemoryVerseBibleVerseRelationship>().FirstOrDefaultAsync(
            x =>
                x.MemoryVersesId == request.FirstId &&
                x.BibleVersesId == request.SecondId,
            cancellationToken: token
        );

        if (rel != null)
            return BasicResponse.WithError(MemoryVerseBibleVerseExists);

        rel = new DbMemoryVerseBibleVerseRelationship
        {
            MemoryVersesId = request.FirstId,
            BibleVersesId = request.SecondId
        };

        await db.AddAsync(rel, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }

    public async Task<BasicResponse> DeleteRelationship(BasicMultipleRelationshipRequest<MemoryVerse, BibleVerse> request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMemoryVerseBibleVerseRelationship? rel = await db.Set<DbMemoryVerseBibleVerseRelationship>().FirstOrDefaultAsync(
            x =>
                x.MemoryVersesId == request.FirstId &&
                x.BibleVersesId == request.SecondId,
            cancellationToken: token
        );

        if (rel == null)
            return BasicResponse.WithError(MemoryVerseBibleVerseNotFound);

        db.Remove(rel);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}