using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Services;

namespace GSBC.ImpactKids.Grpc.Services.SchoolTermServices;

public partial class SchoolTermService(
    GsbcDbContext                        db,
    IEventService<SchoolTerm>            eventService,
    IConverter<DbSchoolTerm, SchoolTerm> converter
) : ISchoolTermsService;