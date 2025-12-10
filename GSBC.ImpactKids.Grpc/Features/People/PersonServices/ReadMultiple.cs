using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.PersonServices;

public partial class PersonService
{
    public async Task<BasicReadMultipleResponse<Person>?> ReadMultiple(
        PeopleRequest request,
        CallContext   context = default
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

        if (request.FamilyId != null)
        {
            query = query.Where(x => x.FamilyId == request.FamilyId)
                .OrderByDescending(x => x.FamilyGuardian)
                .ThenBy(x => x.DateOfBirth)
                .ThenBy(x => x.FirstName);
        }
        else
        {
            query = query.OrderBy(x => x.SchoolGrade!.OrderNumber).ThenBy(x => x.FirstName).ThenBy(x => x.LastName);
        }

        query = query.Paginate(request);

        List<DbPerson> terms = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<Person>
        {
            Success = true,
            Entities = terms.Select(converter.Convert).ToList()
        };
    }
}