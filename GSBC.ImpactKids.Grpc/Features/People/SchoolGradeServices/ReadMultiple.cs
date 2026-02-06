using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.People.SchoolGradeServices;

public partial class SchoolGradeService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<SchoolGrade>> BasicReadMultiple(
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

        await foreach (BasicReadMultipleResponse<SchoolGrade> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}