using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Elvanto;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.Elvanto")]
public interface IElvantoService
{
    Task<ElvantoServicePositionsResponse> GetServicePositions(
        CallContext context = default
    );
}