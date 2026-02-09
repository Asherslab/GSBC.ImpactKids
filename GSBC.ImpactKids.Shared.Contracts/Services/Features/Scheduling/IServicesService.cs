using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;

[Service("gRPC/GSBC.ImpactKids.Services")]
public interface IServicesService
    : IBasicReadMultipleService<Service>,
        ICreateService<CreateServiceRequest>,
        IUpdateService<UpdateServiceRequest>,
        IBasicDeleteService<Service>
{
    Task<BasicReadResponse<Service>> Read(
        ServiceRequest request,
        CallContext    context = default
    );
}