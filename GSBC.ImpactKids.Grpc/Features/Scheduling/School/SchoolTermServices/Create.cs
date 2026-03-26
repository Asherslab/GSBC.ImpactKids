using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.School.SchoolTermServices;

public partial class SchoolTermService
{
    public async Task<BasicReadResponse<Guid?>> Create(CreateSchoolTermRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BasicReadResponse<Guid?>.WithError(SchoolTermNameNull);
        if (request.StartDate == default)
            return BasicReadResponse<Guid?>.WithError(SchoolTermStartDateNull);
        if (request.EndDate == default)
            return BasicReadResponse<Guid?>.WithError(SchoolTermEndDateNull);
        
        DbSchoolTerm term = new()
        {
            Id = Guid.Empty,
            Name = request.Name,

            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        await db.Terms.AddAsync(term, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = term.Id,
            Success = true
        };
    }
}