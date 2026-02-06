using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

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

    public async IAsyncEnumerable<BasicReadMultipleResponse<MemorisationEntry>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbMemorisationEntry> query = db.MemorisationEntries;

        query = query
            .OrderBy(x => x.Service!.Date)
            .ThenBy(x => x.MemoryVerse!.Services.OrderBy(y => y.Date).First());

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.Person!.FirstName.ToLower().Contains(search.ToLower()) ||
                    x.Person!.LastName.ToLower().Contains(search.ToLower())
                );
            }
        }

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<MemorisationEntry> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}