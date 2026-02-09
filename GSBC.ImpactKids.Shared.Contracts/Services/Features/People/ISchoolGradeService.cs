using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("gRPC/GSBC.ImpactKids.Person.SchoolGrade")]
public interface ISchoolGradeService : IBasicReadMultipleService<SchoolGrade>;