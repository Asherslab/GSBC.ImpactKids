using System.Text.Json;
using GSBC.ImpactKids.Grpc.Data.Models.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.ProPresenterMachineServices;

public partial class ProPresenterMachineService
{
    /// <summary>
    /// Writes only the columns a leader owns. The agent writes the status columns on the same
    /// row every 30 seconds, so a whole-entity update here would revert whatever it last reported.
    /// The agent picks the change up on its next configuration pull.
    /// </summary>
    public async Task<BasicResponse> Update(
        UpdateProPresenterMachineRequest request,
        CallContext                      context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        string name = request.Name.Trim();
        string host = request.ProPresenterHost.Trim();

        if (string.IsNullOrEmpty(name))
            return BasicResponse.WithError(ProPresenterMachineNameNull);

        if (string.IsNullOrEmpty(host))
            return BasicResponse.WithError(ProPresenterHostNull);

        if (request.ProPresenterPort is < 1 or > 65535)
            return BasicResponse.WithError(ProPresenterPortInvalid);

        DbProPresenterMachine? machine = await db.ProPresenterMachines
            .FirstOrDefaultAsync(x => x.Id == request.Id, token);

        if (machine == null)
            return BasicResponse.WithError(ProPresenterMachineNotFound);

        machine.Name = name;
        machine.ProPresenterHost = host;
        machine.ProPresenterPort = request.ProPresenterPort;
        machine.ScheduleTimerUuid = string.IsNullOrWhiteSpace(request.ScheduleTimerUuid) ? null : request.ScheduleTimerUuid.Trim();

        // Never null once saved: from here on an empty list means "copy no songs", where null meant
        // "nobody has chosen, copy the Songs library".
        machine.SongLibraryUuidsJson = JsonSerializer.Serialize(request.SongLibraryUuids
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct()
            .Take(100)
            .ToList());

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}
