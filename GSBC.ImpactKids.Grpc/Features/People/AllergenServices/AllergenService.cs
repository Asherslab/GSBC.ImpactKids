using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.People;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.People.AllergenServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class AllergenService(
    GsbcDbContext                    db,
    IConverter<DbAllergen, Allergen> converter
) : IAllergenService;