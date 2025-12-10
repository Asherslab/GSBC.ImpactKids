using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;

[Service("GSBC.ImpactKids.Services")]
public interface IServicesService
{
    Task<BasicResponse?> Create(
        CreateServiceRequest request,
        CallContext          context = default
    );

    Task<BasicReadResponse<Service>?> Read(
        ServiceRequest request,
        CallContext    context = default
    );

    Task<BasicReadMultipleResponse<Service>?> ReadMultiple(
        ServicesRequest request,
        CallContext     context = default
    );

    Task<BasicResponse?> Update(
        UpdateServiceRequest request,
        CallContext          context = default
    );

    Task<BasicResponse?> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}