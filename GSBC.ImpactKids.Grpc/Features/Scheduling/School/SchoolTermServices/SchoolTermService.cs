using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling.School;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.School.SchoolTermServices;

[Authorize(Policy = Policies.EnabledOnly)]
public partial class SchoolTermService(
    GsbcDbContext                        db,
    IEventService<SchoolTerm>            eventService,
    IConverter<DbSchoolTerm, SchoolTerm> converter
) : ISchoolTermsService;