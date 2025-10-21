using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Services.SchoolTermServices;

[Authorize]
public partial class SchoolTermService(
    GsbcDbContext                        db,
    IEventService<SchoolTerm>            eventService,
    IConverter<DbSchoolTerm, SchoolTerm> converter
) : ISchoolTermsService;