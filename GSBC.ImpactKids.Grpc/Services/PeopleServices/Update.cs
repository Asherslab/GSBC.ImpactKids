using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.PeopleServices;

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

        if (request.PreferredName.IsUpdated)
            person.PreferredName = request.PreferredName.Value;

        db.People.Update(person);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(person.Id, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}