using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.People.AllergyServices;

public partial class AllergyService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<Allergy>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbAllergy> query = db.Allergies;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.Allergen!.Label.ToLower().Contains(search.ToLower()) ||
                    x.Notes!.ToLower().Contains(search.ToLower())
                );
            }
        }

        query = query.OrderBy(x => x.Person!.Id);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<Allergy> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}