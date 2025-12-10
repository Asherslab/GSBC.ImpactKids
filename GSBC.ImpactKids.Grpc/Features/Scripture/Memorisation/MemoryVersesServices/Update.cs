using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVersesServices;

public partial class MemoryVersesService
{
    public async Task<BasicResponse?> Update(UpdateMemoryVerseRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMemoryVerse? verse = await db.MemoryVerses
            .Include(x => x.Services)
            .Include(x => x.BibleVerses)
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (verse == null)
            return BasicResponse.WithError(MemoryVerseNotFound);
        
        if (request.ReferenceName.IsUpdated)
        {
            if (string.IsNullOrWhiteSpace(request.ReferenceName.Value))
                return BasicResponse.WithError(MemoryVerseReferenceNameNull);
            verse.ReferenceName = request.ReferenceName.Value;
        }
        
        if (request.Verse.IsUpdated)
        {
            if (string.IsNullOrWhiteSpace(request.Verse.Value))
                return BasicResponse.WithError(MemoryVerseVerseNull);
            verse.Verse = request.Verse.Value;
        }

        if (request.MemoryVerseListId.IsUpdated)
        {
            DbMemoryVerseList? list = await db.MemoryVerseLists
                .FirstOrDefaultAsync(x => x.Id == request.MemoryVerseListId.Value, token);
            
            if (list == null)
                return BasicResponse.WithError(MemoryVerseListNotFound);
            
            verse.MemoryVerseListId = list.Id;
        }

        if (request.ServiceIds.IsUpdated)
        {
            List<DbService> services = await db.Services
                .Where(x => request.ServiceIds.Value.Contains(x.Id))
                .ToListAsync(token);
            if (services.Count != request.ServiceIds.Value.Length)
                return BasicResponse.WithError(ServiceNotFound);

            verse.Services = services;
        }

        if (request.BibleVerseIds.IsUpdated)
        {
            List<DbBibleVerse> verses = await db.BibleVerses
                .Where(x => request.BibleVerseIds.Value.Contains(x.Id))
                .ToListAsync(token);
            if (verses.Count != request.BibleVerseIds.Value.Length)
                return BasicResponse.WithError(BibleVerseNotFound);

            verse.BibleVerses = verses;
        }

        db.MemoryVerses.Update(verse);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(verse.Id, token: token, verse.MemoryVerseListId);

        return new BasicResponse
        {
            Success = true
        };
    }
}