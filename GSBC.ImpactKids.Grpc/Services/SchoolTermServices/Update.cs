using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.SchoolTermServices;

public partial class SchoolTermService
{
    public async Task<BasicResponse?> Update(UpdateSchoolTermRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbSchoolTerm? term = await db.Terms
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (term == null)
            return BasicResponse.WithError(SchoolTermNotFound);
        
        if (request.Name.IsUpdated)
        {
            if (string.IsNullOrWhiteSpace(request.Name.Value))
                return BasicResponse.WithError(SchoolTermNameNull);
            term.Name = request.Name.Value;
        }

        if (request.StartDate.IsUpdated)
        {
            if (request.StartDate.Value == default)
                return BasicResponse.WithError(SchoolTermStartDateNull);
            term.StartDate = request.StartDate.Value;
        }

        if (request.EndDate.IsUpdated)
        {
            if (request.EndDate.Value == default)
                return BasicResponse.WithError(SchoolTermEndDateNull);
            term.EndDate = request.EndDate.Value;
        }

        db.Terms.Update(term);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(term.Id, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}