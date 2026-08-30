using GSBC.ImpactKids.Grpc.Data.Models.People;
using Microsoft.EntityFrameworkCore;
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

        // Resolve the family before building the person, because "in X's family" may have to create
        // that family and back-fill X. Both changes go in on the single SaveChanges below, so the
        // new person can never end up in a household the existing one was not also moved into.
        Guid familyId = request.FamilyId ?? Guid.Empty;

        if (request.FamilyId is null && request.FamilyWithPersonId is { } withPersonId)
        {
            DbPerson? relative = await db.People
                .FirstOrDefaultAsync(x => x.Id == withPersonId, token);

            if (relative is null)
                return BasicReadResponse<Guid?>.WithError(PersonNotFound);

            if (relative.FamilyId == Guid.Empty)
                relative.FamilyId = Guid.NewGuid();

            familyId = relative.FamilyId;
        }

        DbPerson person = new()
        {
            Id = Guid.Empty,

            FirstName = request.FirstName,
            LastName = request.LastName,

            Email = request.Email,
            PhoneNumber = request.PhoneNumber,

            SchoolGradeId = request.SchoolGradeId,
            MediaConsent = request.MediaConsent.ToString(),
            Gender = request.Gender?.ToString(),
            DateOfBirth = request.DateOfBirth,
            FirstTime = request.FirstTime,

            // No family given means no family, not a private household of one. Minting a Guid here
            // was the old way of saying "none", and it now contradicts the new one: Guid.Empty is
            // what Elvanto's "No Family" syncs to, so two answers to the same question would sit in
            // the column at once.
            FamilyId = familyId,
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