using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.AllergenServices;

public partial class AllergenService
{
    public async Task<BasicReadMultipleResponse<Allergen>?> ReadMultiple(
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

        List<DbAllergen> types = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<Allergen>
        {
            Success = true,
            Entities = types.Select(converter.Convert).ToList()
        };
    }
}