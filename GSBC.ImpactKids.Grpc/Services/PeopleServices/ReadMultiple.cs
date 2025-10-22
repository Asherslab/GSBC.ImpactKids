using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.PeopleServices;

public partial class PeopleService
{
    public async Task<BasicReadMultipleResponse<Person>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbPerson> query = db.People;

        if (request.SearchString != null)
        {
            query = query.Where(x =>
                x.FirstName.ToLower().Contains(request.SearchString.ToLower()) ||
                x.LastName.ToLower().Contains(request.SearchString.ToLower()) ||
                x.PreferredName!.ToLower().Contains(request.SearchString.ToLower())
            );
        }

        query = query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName);
        
        query = query.Paginate(request);

        List<DbPerson> terms = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<Person>
        {
            Success = true,
            Entities = terms.Select(converter.Convert).ToList()
        };
    }
}