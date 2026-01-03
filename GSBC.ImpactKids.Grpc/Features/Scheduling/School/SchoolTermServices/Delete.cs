using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.School.SchoolTermServices;

public partial class SchoolTermService
{
    public async Task<BasicResponse> BasicDelete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbSchoolTerm? term = await db.Terms
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (term == null)
            return BasicResponse.WithError(SchoolTermNotFound);

        db.Terms.Remove(term);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(term.Id, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}