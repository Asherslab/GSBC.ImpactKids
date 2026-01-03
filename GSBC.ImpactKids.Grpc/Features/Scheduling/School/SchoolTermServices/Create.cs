using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.School.SchoolTermServices;

public partial class SchoolTermService
{
    public async Task<BasicResponse> Create(CreateSchoolTermRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BasicResponse.WithError(SchoolTermNameNull);
        if (request.StartDate == default)
            return BasicResponse.WithError(SchoolTermStartDateNull);
        if (request.EndDate == default)
            return BasicResponse.WithError(SchoolTermEndDateNull);
        
        DbSchoolTerm term = new()
        {
            Id = Guid.Empty,
            Name = request.Name,

            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        await db.Terms.AddAsync(term, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(term.Id, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}