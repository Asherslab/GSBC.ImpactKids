using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Services.ElvantoServices;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Services.PeopleServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class PeopleService(
    GsbcDbContext                db,
    IEventService<Person>        eventService,
    IConverter<DbPerson, Person> converter,
    ElvantoService               elvantoService
) : IPeopleService;