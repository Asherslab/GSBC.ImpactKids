using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.PersonServices;

public partial class PersonService
{
    public async Task<BasicReadResponse<Person>> Read(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;
        
        DbPerson? person = await db.People
                .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (person == null)
            return BasicReadResponse<Person>.WithError(PersonNotFound);

        return new BasicReadResponse<Person>
        {
            Success = true,
            Entity = converter.Convert(person)
        };
    }
}