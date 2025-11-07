using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.People.PersonServices;

public partial class PersonService
{
    public async Task<BasicResponse?> Create(CreatePersonRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(request.FirstName))
            return BasicResponse.WithError(PersonFirstNameNull);
        if (string.IsNullOrWhiteSpace(request.LastName))
            return BasicResponse.WithError(PersonLastNameNull);
        
        DbPerson person = new()
        {
            Id = Guid.Empty,
            
            FirstName = request.FirstName,
            LastName = request.LastName,
            
            SchoolGradeId = request.SchoolGradeId,
            MediaConsent = request.MediaConsent.ToString(),
            DateOfBirth = request.DateOfBirth,
            FirstTime = request.FirstTime,
            
            FamilyId = request.FamilyId ?? Guid.NewGuid(),
            FamilyGuardian = request.FamilyGuardian
        };

        await db.People.AddAsync(person, token);
        await db.SaveChangesAsync(token);
        await SendEvent(person.Id, person.FamilyId, token);

        return new BasicResponse
        {
            Success = true
        };
    }
}