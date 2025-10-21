using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.SchoolTerms")]
public interface ISchoolTermsService
{
    Task<BasicResponse?> Create(
        CreateSchoolTermRequest request,
        CallContext      context = default
    );

    Task<BasicReadResponse<SchoolTerm>?> Read(
        SchoolTermRequest request,
        CallContext      context = default
    );

    Task<BasicReadMultipleResponse<SchoolTerm>?> ReadMultiple(
        SchoolTermsRequest request,
        CallContext              context = default
    );

    Task<BasicResponse?> Update(
        UpdateSchoolTermRequest request,
        CallContext      context = default
    );

    Task<BasicResponse?> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}