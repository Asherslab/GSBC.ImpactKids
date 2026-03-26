using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.AllergyServices;

public partial class AllergyService
{
    public async Task<BasicReadResponse<Guid?>> Create(CreateAllergyRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (request.AllergenId == null && string.IsNullOrWhiteSpace(request.Notes))
            return BasicReadResponse<Guid?>.WithError(AllergiesMustHaveTypeOrNotes);

        DbPerson? person = await db.People.FirstOrDefaultAsync(x => x.Id == request.PersonId, token);

        if (person == null)
            return BasicReadResponse<Guid?>.WithError(PersonNotFound);

        if (request.AllergenId != null)
        {
            bool allergenExists = await db.Allergens.AnyAsync(x => x.Id == request.AllergenId, token);

            if (!allergenExists)
                return BasicReadResponse<Guid?>.WithError(AllergenNotFound);
        }

        DbAllergy allergy = new()
        {
            Id = Guid.Empty,
            PersonId = request.PersonId,
            AllergenId = request.AllergenId,
            Notes = request.Notes,
            Severe = request.Severe
        };

        await db.Allergies.AddAsync(allergy, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = allergy.Id,
            Success = true
        };
    }
}