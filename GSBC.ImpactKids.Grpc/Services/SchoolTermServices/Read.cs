using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.SchoolTermServices;

public partial class SchoolTermService
{
    [Authorize]
    public async Task<BasicReadResponse<SchoolTerm>?> Read(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbSchoolTerm? term = await db.Terms
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (term == null)
            return BasicReadResponse<SchoolTerm>.WithError(SchoolTermNotFound);

        return new BasicReadResponse<SchoolTerm>
        {
            Success = true,
            Entity = converter.Convert(term)
        };
    }
}