using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.School.SchoolTermServices;

public partial class SchoolTermService
{
    public async Task<BasicReadResponse<SchoolTerm>?> Read(SchoolTermRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;
        
        DbSchoolTerm? term;
        if (!request.ThisTerm)
        {
            term = await db.Terms
                .FirstOrDefaultAsync(x => x.Id == request.Guid, token);
        }
        else
        {
            DateTime? now = DateTime.UtcNow;

            term = await db.Terms
                .FirstOrDefaultAsync(x => x.StartDate <= now && now <= x.EndDate, token);

            if (term == null)
            {
                term = await db.Terms
                    .OrderBy(x => x.StartDate)
                    .FirstOrDefaultAsync(x => now >= x.EndDate, token); // grab next in line term
            }
        }

        if (term == null)
            return BasicReadResponse<SchoolTerm>.WithError(SchoolTermNotFound);

        return new BasicReadResponse<SchoolTerm>
        {
            Success = true,
            Entity = converter.Convert(term)
        };
    }
}