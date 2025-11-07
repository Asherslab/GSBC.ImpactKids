using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.SchoolGradeServices;

public partial class SchoolGradeService
{
    public async Task<BasicReadMultipleResponse<SchoolGrade>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbSchoolGrade> query = db.SchoolGrades;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.Label.ToLower().Contains(search.ToLower())
                );
            }
        }
        
        query = query.OrderBy(x => x.OrderNumber);

        query = query.Paginate(request);

        List<DbSchoolGrade> terms = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<SchoolGrade>
        {
            Success = true,
            Entities = terms.Select(converter.Convert).ToList()
        };
    }
}