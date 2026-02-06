using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.People.AllergenServices;

public partial class AllergenService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<Allergen>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbAllergen> query = db.Allergens;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.Label.ToLower().Contains(search.ToLower())
                );
            }
        }

        query = query.OrderBy(x => x.Label);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<Allergen> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}