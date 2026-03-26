using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.People.PersonServices;

public partial class PersonService
{
    public async Task<BasicReadResponse<Guid?>> Create(CreatePersonRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(request.FirstName))
            return BasicReadResponse<Guid?>.WithError(PersonFirstNameNull);
        if (string.IsNullOrWhiteSpace(request.LastName))
            return BasicReadResponse<Guid?>.WithError(PersonLastNameNull);

        DbPerson person = new()
        {
            Id = Guid.Empty,

            FirstName = request.FirstName,
            LastName = request.LastName,

            Email = request.Email,
            PhoneNumber = request.PhoneNumber,

            SchoolGradeId = request.SchoolGradeId,
            MediaConsent = request.MediaConsent.ToString(),
            DateOfBirth = request.DateOfBirth,
            FirstTime = request.FirstTime,

            FamilyId = request.FamilyId ?? Guid.NewGuid(),
            FamilyGuardian = request.FamilyGuardian
        };

        await db.People.AddAsync(person, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = person.Id,
            Success = true
        };
    }
}