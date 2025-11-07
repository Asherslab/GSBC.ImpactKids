using GSBC.ImpactKids.Shared.Contracts.Entities.People;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("GSBC.ImpactKids.Person.SchoolGrade")]
public interface ISchoolGradeService
{
    Task<BasicReadMultipleResponse<SchoolGrade>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    );
}