using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemorisationEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.MemorisationEntriesServices;

public partial class MemorisationEntriesService
{
    public async Task<BasicReadMultipleResponse<MemorisationEntry>?> ReadMultiple(
        MemorisationEntriesRequest request,
        CallContext                context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbService? service = await db.Services
            .FirstOrDefaultAsync(x => x.Id == request.ServiceId, cancellationToken: token);

        if (service == null)
            return new BasicReadMultipleResponse<MemorisationEntry>
            {
                Success = false,
                Error = ServiceNotFound
            };

        IQueryable<DbPerson> personQuery = db.People;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                personQuery = personQuery.Where(x =>
                    x.FirstName.ToLower().Contains(search.ToLower()) ||
                    x.LastName.ToLower().Contains(search.ToLower()) ||
                    x.PreferredName!.ToLower().Contains(search.ToLower())
                );
            }
        }

        personQuery = personQuery.OrderBy(x => x.FirstName).ThenBy(x => x.LastName);

        personQuery = personQuery.Paginate(request);

        List<DbPerson> people = await personQuery.ToListAsync(token);

        if (people.Count == 0)
        {
            // no people found, usually due to a search query not having any values.
            // let's return a successful but empty list.
            return new BasicReadMultipleResponse<MemorisationEntry>
            {
                Success = true,
                Entities = new List<MemorisationEntry>()
            };
        }

        IQueryable<DbMemorisationEntry> query = db.MemorisationEntries
            .Include(x => x.Person)
            .Include(x => x.Service);

        List<Guid> peopleIds = people.Select(x => x.Id).ToList();

        query = query
            // .Where(x => x.ServiceId == request.ServiceId)
            // don't filter by service because we're going to use past services to determine some details
            .Where(x => x.MemoryVerseId == request.MemoryVerseId)
            .Where(x => peopleIds.Contains(x.PersonId));

        List<DbMemorisationEntry> entries = await query
            .OrderBy(x => x.Person!.FirstName)
            .ThenBy(x => x.Person!.LastName)
            .ToListAsync(token);

        List<DbMemorisationEntry> entriesForRequest = entries.Where(x => x.ServiceId == request.ServiceId).ToList();

        if (entriesForRequest.Count > people.Count)
        {
            return new BasicReadMultipleResponse<MemorisationEntry>
            {
                Success = false,
                Error = "this is a really odd error... contact asher?"
            };
        }

        List<DbMemorisationEntry> newEntries = [];
        foreach (DbPerson dbPerson in people)
        {
            if (entriesForRequest.Any(x => x.PersonId == dbPerson.Id))
                continue;

            DbMemorisationEntry newEntry = new()
            {
                Id = Guid.Empty,

                PersonId = dbPerson.Id,
                Person = dbPerson,
                ServiceId = request.ServiceId,
                Service = service,
                MemoryVerseId = request.MemoryVerseId,
            };

            newEntries.Add(newEntry);
        }

        if (newEntries.Count != 0)
        {
            await db.MemorisationEntries.AddRangeAsync(newEntries, token);
            await db.SaveChangesAsync(token);
            await eventService.SendUpdatedEvent(Guid.Empty, token: token);
        }

        entries.AddRange(newEntries);
        entriesForRequest = entries.Where(x => x.ServiceId == request.ServiceId).ToList();
        List<DbMemorisationEntry> entriesNotForThisRequest =
            entries.Where(x => x.ServiceId != request.ServiceId).ToList();

        List<MemorisationEntry> dtoEntries = entriesForRequest
            .Select(converter.Convert)
            .OrderBy(x => x.Person!.FirstName)
            .ThenBy(x => x.Person!.LastName)
            .ToList();
        
        foreach (MemorisationEntry entry in dtoEntries)
        {
            entry.VerseHasBeenRecitedBefore = entriesNotForThisRequest
                .Where(x => x.PersonId == entry.PersonId) // the entries relate to this entry's person
                .Where(x => x.Service!.Date < entry.Service!.Date)
                .Any(x => x.VerseRecited);
        }

        return new BasicReadMultipleResponse<MemorisationEntry>
        {
            Success = true,
            Entities = dtoEntries
        };
    }
}