using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.People.PersonServices;

public partial class PersonService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<Person>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbPerson> query = db.People;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.FirstName.ToLower().Contains(search.ToLower()) ||
                    x.LastName.ToLower().Contains(search.ToLower())
                );
            }
        }

        query = query
            .OrderBy(x => x.SchoolGrade!.OrderNumber)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.LastName);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<Person> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}