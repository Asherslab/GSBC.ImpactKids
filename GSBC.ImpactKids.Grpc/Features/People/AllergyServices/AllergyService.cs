using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

namespace GSBC.ImpactKids.Grpc.Features.People.AllergyServices;

public partial class AllergyService(
    GsbcDbContext                  db,
    IEventService<Allergy>         eventService,
    IConverter<DbAllergy, Allergy> converter
) : IAllergyService;