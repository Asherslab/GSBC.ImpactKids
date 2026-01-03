using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("GSBC.ImpactKids.Person")]
public interface IPersonService
    : IBasicReadMultipleService<Person>,
        ICreateService<CreatePersonRequest>,
        IUpdateService<UpdatePersonRequest>,
        IBasicDeleteService<Person>
{
    Task<BasicResponse> SyncWithElvanto(
        CallContext context = default
    );

    Task<BasicReadResponse<Person>> Read(
        BasicReadRequest request,
        CallContext      context = default
    );

    Task<BasicReadMultipleResponse<Person>> ReadMultiple(
        PeopleRequest request,
        CallContext   context = default
    );
}