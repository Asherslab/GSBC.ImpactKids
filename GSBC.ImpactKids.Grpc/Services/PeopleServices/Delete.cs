using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.PeopleServices;

public partial class PeopleService
{
    public async Task<BasicResponse?> Delete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbPerson? person = await db.People
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (person == null)
            return BasicResponse.WithError(PersonNotFound);

        db.People.Remove(person);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(person.Id, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}