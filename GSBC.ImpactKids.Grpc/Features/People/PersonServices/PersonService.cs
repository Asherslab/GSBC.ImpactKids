using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

namespace GSBC.ImpactKids.Grpc.Features.People.PersonServices;

public partial class PersonService(
    GsbcDbContext                db,
    IEventService<Person>        eventService,
    IConverter<DbPerson, Person> converter
) : IPersonService;