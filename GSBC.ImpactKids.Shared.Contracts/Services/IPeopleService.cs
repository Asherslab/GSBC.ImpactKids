using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.People;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.People")]
public interface IPeopleService
{
    Task<BasicResponse?> Create(
        CreatePersonRequest request,
        CallContext             context = default
    );

    Task<BasicReadResponse<Person>?> Read(
        BasicReadRequest request,
        CallContext       context = default
    );

    Task<BasicReadMultipleResponse<Person>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext        context = default
    );

    Task<BasicResponse?> Update(
        UpdatePersonRequest request,
        CallContext             context = default
    );

    Task<BasicResponse?> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}