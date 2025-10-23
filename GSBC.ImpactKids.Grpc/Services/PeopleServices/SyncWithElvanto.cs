using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.PeopleServices;

public partial class PeopleService
{
    public async Task<BasicResponse?> SyncWithElvanto(CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        BasicReadMultipleResponse<DbPerson> resp = await elvantoService
            .GetImpactKidsAgePeople(token);

        List<string> personElvantoIds = resp.Entities.Select(x => x.ElvantoId!).ToList();

        List<DbPerson> peopleWithElvantoIds = await db.People
            .Where(x => personElvantoIds.Contains(x.ElvantoId!))
            .ToListAsync(token);

        IEnumerable<DbPerson> peopleNotInDb =
            resp.Entities.Where(x => peopleWithElvantoIds.All(y => y.ElvantoId != x.ElvantoId));

        await db.People.AddRangeAsync(peopleNotInDb, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(Guid.Empty, token: token);

        return new BasicResponse
        {
            Success = true
        };
    }
}