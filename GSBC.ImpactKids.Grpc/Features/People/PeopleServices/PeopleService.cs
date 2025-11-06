using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices;
using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.People;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.People.PeopleServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class PeopleService(
    GsbcDbContext                db,
    IEventService<Person>        eventService,
    IConverter<DbPerson, Person> converter,
    ElvantoService               elvantoService
) : IPeopleService
{
    private async Task SendEvent(Guid personId, Guid familyId, CancellationToken token = default)
    {
        await eventService.SendUpdatedEvent(personId, token: token, familyId);
    }
}