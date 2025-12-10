using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.People;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.People.MedicalTypeServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class MedicalTypeService(
    GsbcDbContext                          db,
    IConverter<DbMedicalType, MedicalType> converter
) : IMedicalTypeService;