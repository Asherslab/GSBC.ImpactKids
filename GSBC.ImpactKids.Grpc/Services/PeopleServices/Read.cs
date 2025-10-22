using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.PeopleServices;

public partial class PeopleService
{
    public async Task<BasicReadResponse<Person>?> Read(BasicReadRequest request, CallContext context = default)
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