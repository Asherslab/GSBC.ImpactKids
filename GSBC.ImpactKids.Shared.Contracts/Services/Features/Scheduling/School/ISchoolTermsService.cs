using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling.School;

[Service("GSBC.ImpactKids.SchoolTerms")]
public interface ISchoolTermsService 
    : IBasicReadMultipleService<SchoolTerm>
{
    Task<BasicResponse> Create(
        CreateSchoolTermRequest request,
        CallContext      context = default
    );

    Task<BasicReadResponse<SchoolTerm>> Read(
        SchoolTermRequest request,
        CallContext      context = default
    );

    Task<BasicReadMultipleResponse<SchoolTerm>> ReadMultiple(
        SchoolTermsRequest request,
        CallContext              context = default
    );

    Task<BasicResponse> Update(
        UpdateSchoolTermRequest request,
        CallContext      context = default
    );

    Task<BasicResponse> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}