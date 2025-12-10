using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVersesServices;

public partial class MemoryVersesService
{
    public async Task<BasicResponse?> Create(CreateMemoryVerseRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(request.ReferenceName))
            return BasicResponse.WithError(MemoryVerseReferenceNameNull);

        if (string.IsNullOrWhiteSpace(request.Verse))
            return BasicResponse.WithError(MemoryVerseVerseNull);

        DbMemoryVerseList? list = await db.MemoryVerseLists
            .FirstOrDefaultAsync(x => x.Id == request.MemoryVerseListId, token);
        if (list == null)
            return BasicResponse.WithError(MemoryVerseListNotFound);

        List<DbService> services = await db.Services
            .Where(x => request.ServiceIds.Contains(x.Id))
            .ToListAsync(token);
        if (services.Count != request.ServiceIds.Count)
            return BasicResponse.WithError(ServiceNotFound);

        List<DbBibleVerse> bibleVerses = await db.BibleVerses
            .Where(x => request.BibleVerseIds.Contains(x.Id))
            .ToListAsync(token);
        if (bibleVerses.Count != request.BibleVerseIds.Count)
            return BasicResponse.WithError(BibleVerseNotFound);

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
        await eventService.SendUpdatedEvent(verse.Id, token: token, verse.MemoryVerseListId);

        return new BasicResponse
        {
            Success = true
        };
    }
}