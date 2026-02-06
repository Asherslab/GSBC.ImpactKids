using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.People;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.People.PersonServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class PersonService(
    GsbcDbContext                db,
    IEventService<Person>        eventService,
    IConverter<DbPerson, Person> converter,
    ElvantoService               elvantoService
) : IPersonService;