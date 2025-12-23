using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemorisationEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemorisationEntriesServices;

public partial class MemorisationEntriesService
{
    private static readonly string[] SchoolGrades =
    [
        "Nursery/Pre-school",
        "Kindergarten",
        "Prep",
        "1",
        "2",
        "3",
        "4",
        "5",
        "6"
    ];
    
    public async Task<BasicReadMultipleResponse<MemorisationEntry>?> ReadMultiple(
        MemorisationEntriesRequest request,
        CallContext                context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbVirtualMemorisationEntry> entriesQuery = db.VirtualMemorisationEntries;

        if (request.IncludePerson)
            entriesQuery = entriesQuery.Include(x => x.Person);
        if (request.IncludeService)
            entriesQuery = entriesQuery.Include(x => x.Service);
        if (request.IncludeMemoryVerse)
            entriesQuery = entriesQuery.Include(x => x.MemoryVerse);

        if (request.PersonId != null)
        {
            entriesQuery = entriesQuery.Where(x => x.PersonId == request.PersonId);
        }
        else
        {
            entriesQuery = entriesQuery.Where(x =>
                x.Person!.SchoolGrade != null &&
                SchoolGrades.Contains(x.Person.SchoolGrade!.Label)
            );
        }

        if (request.ServiceId != null)
        {
            entriesQuery = entriesQuery.Where(x => x.ServiceId == request.ServiceId);
        }

        if (request.SchoolTermId != null)
        {
            entriesQuery = entriesQuery.Where(x => x.Service!.SchoolTermId == request.SchoolTermId);
        }

        if (request.CurrentSchoolTerm)
        {
            entriesQuery = entriesQuery.Where(x =>
                x.Service!.SchoolTerm!.StartDate <= DateTime.UtcNow &&
                x.Service!.SchoolTerm!.EndDate >= DateTime.UtcNow
            );
        }

        if (request.MemoryVerseId != null)
        {
            entriesQuery = entriesQuery.Where(x => x.MemoryVerseId == request.MemoryVerseId);
            
            
        }
        
        if (request.PersonId == null)
        {
            if (request.MemoryVerseId != null)
            {
                entriesQuery = entriesQuery
                    .OrderBy(x => x.Person!.FirstName)
                    .ThenBy(x => x.Person!.LastName);
            }
            else
            {
                entriesQuery = entriesQuery
                    .OrderBy(x => x.Person!.FirstName)
                    .ThenBy(x => x.Person!.LastName)
                    .ThenBy(x => x.Service!.Date)
                    .ThenBy(x => x.MemoryVerse!.Services.OrderBy(y => y.Date).First());
            }
        }
        else
        {
            entriesQuery = entriesQuery
                .OrderBy(x => x.Service!.Date)
                .ThenBy(x => x.MemoryVerse!.Services.OrderBy(y => y.Date).First());
        }

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                entriesQuery = entriesQuery.Where(x =>
                    x.Person!.FirstName.ToLower().Contains(search.ToLower()) ||
                    x.Person!.LastName.ToLower().Contains(search.ToLower())
                );
            }
        }

        entriesQuery = entriesQuery.Paginate(request);
        List<DbVirtualMemorisationEntry> entries = await entriesQuery.ToListAsync(token);

        return new BasicReadMultipleResponse<MemorisationEntry>
        {
            Success = true,
            Entities = entries.Select(converter.Convert).ToImmutableList()
        };
    }
}