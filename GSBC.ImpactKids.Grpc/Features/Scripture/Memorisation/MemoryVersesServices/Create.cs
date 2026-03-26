using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVersesServices;

public partial class MemoryVersesService
{
    public async Task<BasicReadResponse<Guid?>> Create(CreateMemoryVerseRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(request.ReferenceName))
            return BasicReadResponse<Guid?>.WithError(MemoryVerseReferenceNameNull);

        if (string.IsNullOrWhiteSpace(request.Verse))
            return BasicReadResponse<Guid?>.WithError(MemoryVerseVerseNull);

        DbMemoryVerseList? list = await db.MemoryVerseLists
            .FirstOrDefaultAsync(x => x.Id == request.MemoryVerseListId, token);
        if (list == null)
            return BasicReadResponse<Guid?>.WithError(MemoryVerseListNotFound);

        List<DbService> services = await db.Services
            .Where(x => request.ServiceIds.Contains(x.Id))
            .ToListAsync(token);
        if (services.Count != request.ServiceIds.Count)
            return BasicReadResponse<Guid?>.WithError(ServiceNotFound);

        List<DbBibleVerse> bibleVerses = await db.BibleVerses
            .Where(x => request.BibleVerseIds.Contains(x.Id))
            .ToListAsync(token);
        if (bibleVerses.Count != request.BibleVerseIds.Count)
            return BasicReadResponse<Guid?>.WithError(BibleVerseNotFound);

        DbMemoryVerse verse = new()
        {
            Id = Guid.Empty,

            ReferenceName = request.ReferenceName,
            Verse = request.Verse,
            MemoryVerseListId = list.Id,

            Services = services,
            BibleVerses = bibleVerses
        };

        await db.MemoryVerses.AddAsync(verse, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = verse.Id,
            Success = true
        };
    }
}