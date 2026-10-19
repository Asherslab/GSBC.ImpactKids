using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.ProPresenter;

/// <summary>
/// The admin end of the ProPresenter machines: approve a new one, configure it, revoke it. Leader
/// only, by carrying no authorization at all on the server. A machine never reaches this - it has
/// its own service, <see cref="IProPresenterAgentService"/>, on its own route.
/// </summary>
[Service("gRPC/GSBC.ImpactKids.ProPresenter.Machines")]
public interface IProPresenterMachineService :
    IBasicReadMultipleService<ProPresenterMachine>,
    IUpdateService<UpdateProPresenterMachineRequest>,
    IBasicDeleteService<ProPresenterMachine>
{
    /// <summary>
    /// Creates the machine and returns a single-use code for the connect page to hand back to the
    /// agent on its loopback address. The code is worthless without the verifier the agent kept,
    /// and it expires within a couple of minutes.
    /// </summary>
    Task<BasicReadResponse<string>> Approve(
        ApproveProPresenterMachineRequest request,
        CallContext                       context = default
    );
}
