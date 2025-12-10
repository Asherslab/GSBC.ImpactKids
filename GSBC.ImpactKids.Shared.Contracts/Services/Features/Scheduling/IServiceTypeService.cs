using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services.ServiceTypes;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;

[Service("GSBC.ImpactKids.Services.ServiceTypes")]
public interface IServiceTypeService
{
    Task<BasicResponse?> Create(
        CreateServiceTypeRequest request,
        CallContext              context = default
    );

    Task<BasicReadResponse<ServiceType>?> Read(
        BasicReadRequest request,
        CallContext      context = default
    );

    Task<BasicReadMultipleResponse<ServiceType>?> ReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    );

    Task<BasicResponse?> Update(
        UpdateServiceTypeRequest request,
        CallContext              context = default
    );

    Task<BasicResponse?> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}