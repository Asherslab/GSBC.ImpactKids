using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.Services")]
public interface IServicesService
{
    Task<BasicResponse?> Create(
        CreateServiceRequest request,
        CallContext          context = default
    );

    Task<BasicReadResponse<Service>?> Read(
        BasicReadRequest request,
        CallContext      context = default
    );

    Task<BasicReadResponse<Service>?> ReadByDate(
        ServiceByDateRequest request,
        CallContext          context = default
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