using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.PeopleServices;

public partial class PeopleService
{
    public async Task<BasicResponse?> Update(UpdatePersonRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbPerson? person = await db.People
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (person == null)
            return BasicResponse.WithError(PersonNotFound);
        
        if (request.FirstName.IsUpdated)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName.Value))
                return BasicResponse.WithError(PersonFirstNameNull);
            person.FirstName = request.FirstName.Value;
        }

        if (request.LastName.IsUpdated)
        {
            if (string.IsNullOrWhiteSpace(request.LastName.Value))
                return BasicResponse.WithError(PersonLastNameNull);
            person.LastName = request.LastName.Value;
        }

        if (request.SchoolGradeId.IsUpdated)
        {
            if (request.SchoolGradeId.Value != null)
            {
                DbSchoolGrade? grade = await db.SchoolGrades
                    .FirstOrDefaultAsync(x => x.Id == request.SchoolGradeId.Value, token);
                
                if (grade == null)
                    return BasicResponse.WithError(SchoolGradeNotFound);
            }
            
            person.SchoolGradeId = request.SchoolGradeId.Value;
        }

        if (request.MediaConsent.IsUpdated)
        {
            // convoluted but ensures that we have a legitimate value saved
            if (!Enum.TryParse(request.MediaConsent.Value.ToString(), out MediaConsent consent))
                return BasicResponse.WithError(MediaConsentNotFound);
            
            person.MediaConsent = consent.ToString();
        }

        if (request.DateOfBirth.IsUpdated)
        {
            person.DateOfBirth = request.DateOfBirth.Value;
        }

        if (request.FirstTime.IsUpdated)
        {
            person.FirstTime = request.FirstTime.Value;
        }

        if (request.FamilyId.IsUpdated)
        {
            person.FamilyId = request.FamilyId.Value;
        }

        if (request.FamilyGuardian.IsUpdated)
        {
            person.FamilyGuardian = request.FamilyGuardian.Value;
        }

        db.People.Update(person);
        await db.SaveChangesAsync(token);
        await SendEvent(person.Id, person.FamilyId, token);

        return new BasicResponse
        {
            Success = true
        };
    }
}