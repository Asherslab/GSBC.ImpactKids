using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>An item - and, for a section, the items under it.</summary>
    public async Task<BasicResponse> RemoveItem(BasicReadRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfItem(request.Guid, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            if (Locate(schedule, request.Guid) is not var (list, item))
                return BasicResponse.WithError(ScheduleItemNotFound);

            foreach (DbScheduleItem child in item.Children.ToList())
                db.ScheduleItems.Remove(child);

            list.Remove(item);
            db.ScheduleItems.Remove(item);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
