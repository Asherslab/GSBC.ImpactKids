using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Elvanto;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

public partial class ElvantoService
{
    [Authorize]
    public Task<ElvantoReportResponse?> GetElvantoReport(string reportLabel, CallContext context = default)
    {
        return Task.FromResult(config.Reports.FirstOrDefault(x => x.Label == reportLabel));
    }
}