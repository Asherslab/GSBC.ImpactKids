using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services.ServiceTypes;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;

[Service("gRPC/GSBC.ImpactKids.Services.ServiceTypes")]
public interface IServiceTypeService
    : IBasicReadMultipleService<ServiceType>,
        ICreateService<CreateServiceTypeRequest>,
        IUpdateService<UpdateServiceTypeRequest>,
        IBasicDeleteService<ServiceType>
{
    Task<BasicReadResponse<ServiceType>> Read(
        BasicReadRequest request,
        CallContext      context = default
    );
}