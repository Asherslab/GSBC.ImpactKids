using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Services.PeopleServices;

public partial class PeopleService
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
            PreferredName = request.PreferredName
        };

        await db.People.AddAsync(person, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(person.Id, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}