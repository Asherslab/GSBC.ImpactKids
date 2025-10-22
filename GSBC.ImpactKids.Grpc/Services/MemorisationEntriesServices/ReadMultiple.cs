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

        IQueryable<DbPerson> personQuery = db.People;

        if (request.SearchString != null)
        {
            personQuery = personQuery.Where(x =>
                x.FirstName.ToLower().Contains(request.SearchString.ToLower()) ||
                x.LastName.ToLower().Contains(request.SearchString.ToLower()) ||
                x.PreferredName!.ToLower().Contains(request.SearchString.ToLower())
            );
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
            .Include(x => x.Person);

        List<Guid> peopleIds = people.Select(x => x.Id).ToList();
        
        query = query
            .Where(x => x.ServiceId == request.ServiceId)
            .Where(x => x.MemoryVerseId == request.MemoryVerseId)
            .Where(x => peopleIds.Contains(x.PersonId));

        List<DbMemorisationEntry> entries = await query.ToListAsync(token);

        if (entries.Count > people.Count)
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
            if (entries.Any(x => x.PersonId == dbPerson.Id))
                continue;

            DbMemorisationEntry newEntry = new()
            {
                Id = Guid.Empty,

                PersonId = dbPerson.Id,
                Person = dbPerson,
                ServiceId = request.ServiceId,
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

        return new BasicReadMultipleResponse<MemorisationEntry>
        {
            Success = true,
            Entities = entries.Select(converter.Convert).ToList()
        };
    }
}