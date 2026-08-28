using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

namespace GSBC.ImpactKids.Grpc.Features.People.SchoolGradeServices;

public partial class SchoolGradeService(
    GsbcDbContext                          db,
    IConverter<DbSchoolGrade, SchoolGrade> converter
) : ISchoolGradeService;