using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.ProPresenterMachineServices;

public partial class ProPresenterMachineService
{
    /// <summary>
    /// Revocation. A hard delete, unlike attendance: the row is a credential, not evidence, and
    /// removing it is what makes the machine's secret and every token it holds worthless.
    /// </summary>
    public async Task<BasicResponse> BasicDelete(
        BasicReadRequest request,
        CallContext      context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        int deleted = await db.ProPresenterMachines
            .Where(x => x.Id == request.Guid)
            .ExecuteDeleteAsync(token);

        if (deleted == 0)
            return BasicResponse.WithError(ProPresenterMachineNotFound);

        // Immediate in this process rather than on the next tick: the machine's live access token
        // stops verifying on its very next call.
        await signingKeys.RefreshAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}
