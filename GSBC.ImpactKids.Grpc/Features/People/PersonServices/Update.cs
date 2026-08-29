using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.PersonServices;

public partial class PersonService
{
    public async Task<BasicResponse> Update(UpdatePersonRequest request, CallContext context = default)
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

        if (request.Email.IsUpdated)
        {
            if (string.IsNullOrWhiteSpace(request.Email.Value))
                request.Email.Value = null;
            person.Email = request.Email.Value;
        }

        if (request.PhoneNumber.IsUpdated)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber.Value))
                request.PhoneNumber.Value = null;
            person.PhoneNumber = request.PhoneNumber.Value;
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

        if (request.Gender.IsUpdated)
        {
            // Null is a legitimate value here - clearing the select puts the person back to "not
            // told" - so unlike MediaConsent there is nothing to reject, only an enum to name.
            person.Gender = request.Gender.Value?.ToString();
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
            // Clearing the family means no family. It used to mint a fresh Guid, so "remove this
            // person from their family" quietly put them in a new one of their own - which reads
            // identically in the column and differently to everything that groups on it.
            person.FamilyId = request.FamilyId.Value ?? Guid.Empty;
        }

        if (request.FamilyGuardian.IsUpdated)
        {
            person.FamilyGuardian = request.FamilyGuardian.Value;
        }

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}