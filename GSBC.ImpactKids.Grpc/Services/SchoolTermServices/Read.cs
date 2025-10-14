using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.SchoolTermServices;

public partial class SchoolTermService
{
    [Authorize]
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
            DateTime? now = DateTime.Now;

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