using System.Collections.Immutable;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.AllergyServices;

public partial class AllergyService
{
    public async Task<BasicReadMultipleResponse<Allergy>> BasicReadMultiple(
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

        List<DbAllergy> types = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<Allergy>
        {
            Success = true,
            Entities = types.Select(converter.Convert).ToImmutableList()
        };
    }
}