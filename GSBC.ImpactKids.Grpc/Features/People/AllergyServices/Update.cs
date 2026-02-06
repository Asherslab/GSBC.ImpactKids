using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.AllergyServices;

public partial class AllergyService
{
    public async Task<BasicResponse> Update(UpdateAllergyRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbAllergy? allergy = await db.Allergies.FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (allergy == null)
            return BasicResponse.WithError(MedicalNoteNotFound);

        if (request.AllergenId.IsUpdated)
        {
            if (request.AllergenId.Value != null)
            {
                DbAllergen? allergen =
                    await db.Allergens.FirstOrDefaultAsync(x => x.Id == request.AllergenId.Value, token);

                if (allergen == null)
                    return BasicResponse.WithError(AllergenNotFound);
            }

            allergy.AllergenId = request.AllergenId.Value;
        }

        if (request.Notes.IsUpdated)
        {
            allergy.Notes = request.Notes.Value;

            if (string.IsNullOrWhiteSpace(allergy.Notes))
                allergy.Notes = null;
        }

        if (request.Severe.IsUpdated)
        {
            allergy.Severe = request.Severe.Value;
        }

        db.Allergies.Update(allergy);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}