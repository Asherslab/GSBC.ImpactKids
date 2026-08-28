using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Elvanto;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

public partial class ElvantoService
{
    // No attribute: the fallback policy is EnabledOnly. This used to carry a bare
    // [Authorize] which, stacked on the class level EnabledOnly, meant the same thing - on
    // its own it would mean merely "signed in", which is weaker than it has ever been.
    public Task<ElvantoReportResponse?> GetElvantoReport(string reportLabel, CallContext context = default)
    {
        return Task.FromResult(config.Reports.FirstOrDefault(x => x.Label == reportLabel));
    }
}