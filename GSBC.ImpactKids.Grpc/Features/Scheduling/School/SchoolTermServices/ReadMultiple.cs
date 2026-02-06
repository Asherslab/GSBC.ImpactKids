using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.School.SchoolTermServices;

public partial class SchoolTermService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<SchoolTerm>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbSchoolTerm> query = db.Terms;

        if (request.SearchString != null)
        {
            query = query.Where(x => x.Name.ToLower().Contains(request.SearchString.ToLower()));
        }

        query = query.OrderBy(x => x.StartDate);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<SchoolTerm> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}