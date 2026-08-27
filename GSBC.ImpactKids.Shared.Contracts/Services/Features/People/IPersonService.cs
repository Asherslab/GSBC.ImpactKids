using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.People;

[Service("gRPC/GSBC.ImpactKids.Person")]
public interface IPersonService
    : IBasicReadMultipleService<Person>,
        ICreateService<CreatePersonRequest>,
        IUpdateService<UpdatePersonRequest>,
        IBasicDeleteService<Person>
{
    Task<BasicReadResponse<Person>> Read(
        BasicReadRequest request,
        CallContext      context = default
    );
}