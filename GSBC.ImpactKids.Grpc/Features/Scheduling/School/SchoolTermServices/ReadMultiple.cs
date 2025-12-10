using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.School.SchoolTermServices;

public partial class SchoolTermService
{
    public async Task<BasicReadMultipleResponse<SchoolTerm>?> ReadMultiple(
        SchoolTermsRequest request,
        CallContext        context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbSchoolTerm> query = db.Terms;

        if (request.SearchString != null)
        {
            query = query.Where(x => x.Name.ToLower().Contains(request.SearchString.ToLower()));
        }

        if (request.Year != null)
        {
            query = query.Where(x =>
                x.StartDate.Year == request.Year.Value ||
                x.EndDate.Year == request.Year.Value
            );
        }

        query = query.OrderBy(x => x.StartDate);
        
        query = query.Paginate(request);

        List<DbSchoolTerm> terms = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<SchoolTerm>
        {
            Success = true,
            Entities = terms.Select(converter.Convert).ToList()
        };
    }
}