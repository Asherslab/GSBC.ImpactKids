using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<Schedule>>
        BasicReadMultiple(BasicReadMultipleRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        await foreach (BasicReadMultipleResponse<Schedule> response in
                       WholeSchedules.OrderBy(x => x.Id).Paginate(request).ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}
