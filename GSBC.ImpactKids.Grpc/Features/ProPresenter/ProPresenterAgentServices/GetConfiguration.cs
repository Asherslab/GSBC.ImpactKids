using GSBC.ImpactKids.Grpc.Conversion;
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

        var machine = await db.ProPresenterMachines
            .AsNoTracking()
            .Where(x => x.Id == machineId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.ProPresenterHost,
                x.ProPresenterPort,
                x.ScheduleTimerUuid,
                x.SongLibraryUuidsJson,
                x.LibrariesJson
            })
            .FirstOrDefaultAsync(token);

        // Deleted between the token being minted and now. The token stops verifying within one key
        // refresh anyway; this just answers the call in the meantime.
        if (machine == null)
            return BasicReadResponse<ProPresenterAgentConfiguration>.WithError(ProPresenterMachineNotFound);

        return new BasicReadResponse<ProPresenterAgentConfiguration>
        {
            Success = true,
            Entity = new ProPresenterAgentConfiguration
            {
                MachineId = machine.Id,
                Name = machine.Name,
                ProPresenterHost = machine.ProPresenterHost,
                ProPresenterPort = machine.ProPresenterPort,
                ScheduleTimerUuid = machine.ScheduleTimerUuid,
                SongLibraryUuids = ProPresenterMachineConverter
                    .SongLibraries(machine.SongLibraryUuidsJson, machine.LibrariesJson)
                    .ToList()
            }
        };
    }
}
