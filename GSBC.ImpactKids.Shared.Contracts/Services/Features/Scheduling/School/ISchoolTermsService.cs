using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling.School;

[Service("gRPC/GSBC.ImpactKids.SchoolTerms")]
public interface ISchoolTermsService
    : IBasicReadMultipleService<SchoolTerm>,
        ICreateService<CreateSchoolTermRequest>,
        IUpdateService<UpdateSchoolTermRequest>,
        IBasicDeleteService<SchoolTerm>
{
    Task<BasicReadResponse<SchoolTerm>> Read(
        SchoolTermRequest request,
        CallContext       context = default
    );
}