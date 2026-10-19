using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.ProPresenterAgentServices;

public partial class ProPresenterAgentService
{
    public async Task<BasicReadResponse<ProPresenterAgentConfiguration>> GetConfiguration(
        ProPresenterAgentRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        Guid? machineId = CallingMachine(context);

        if (machineId == null)
            return BasicReadResponse<ProPresenterAgentConfiguration>.WithError(PermissionDenied);

        ProPresenterAgentConfiguration? configuration = await db.ProPresenterMachines
            .AsNoTracking()
            .Where(x => x.Id == machineId)
            .Select(x => new ProPresenterAgentConfiguration
            {
                MachineId = x.Id,
                Name = x.Name,
                ProPresenterHost = x.ProPresenterHost,
                ProPresenterPort = x.ProPresenterPort,
                ScheduleTimerUuid = x.ScheduleTimerUuid
            })
            .FirstOrDefaultAsync(token);

        // Deleted between the token being minted and now. The token stops verifying within one key
        // refresh anyway; this just answers the call in the meantime.
        if (configuration == null)
            return BasicReadResponse<ProPresenterAgentConfiguration>.WithError(ProPresenterMachineNotFound);

        return new BasicReadResponse<ProPresenterAgentConfiguration>
        {
            Success = true,
            Entity = configuration
        };
    }
}
