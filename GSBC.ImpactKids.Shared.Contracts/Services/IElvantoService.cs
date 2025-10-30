using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Elvanto;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Elvanto;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.Elvanto")]
public interface IElvantoService
{
    Task<ElvantoServicePositionsResponse> GetServicePositions(
        ServicePositionsRequest request,
        CallContext context = default
    );
}