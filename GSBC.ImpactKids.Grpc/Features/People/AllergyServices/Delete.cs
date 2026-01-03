using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.AllergyServices;

public partial class AllergyService
{
    public async Task<BasicResponse> BasicDelete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbAllergy? allergy = await db.Allergies
            .Include(x => x.Person)
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (allergy?.Person == null)
            return BasicResponse.WithError(AllergyNotFound);

        db.Allergies.Remove(allergy);
        await db.SaveChangesAsync(token);
        await SendEvent(allergy.Person.Id, allergy.Person.FamilyId, token);

        return new BasicResponse
        {
            Success = true
        };
    }
}