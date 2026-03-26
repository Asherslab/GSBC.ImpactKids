using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Elvanto;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Elvanto;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Elvanto;

[Service("gRPC/GSBC.ImpactKids.Elvanto")]
public interface IElvantoService
{
    Task<ElvantoServicePositionsResponse> GetServicePositions(
        ServicePositionsRequest request,
        CallContext             context = default
    );

    Task<ElvantoReportResponse?> GetElvantoReport(
        string      reportLabel,
        CallContext context = default
    );
}