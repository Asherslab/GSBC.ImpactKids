using GSBC.ImpactKids.Shared.Contracts.Entities.People;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("GSBC.ImpactKids.People.SchoolGrades")]
public interface ISchoolGradesService
{
    Task<BasicReadMultipleResponse<SchoolGrade>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    );
}