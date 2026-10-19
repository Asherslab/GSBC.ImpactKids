using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    public async Task<BasicResponse> RenameGroup(RenameScheduleGroupRequest request, CallContext context = default)
    {
        string name = request.Name.Trim();

        if (name.Length == 0)
            return BasicResponse.WithError(ScheduleGroupNameNull);

        Guid? scheduleId = await ScheduleOfGroup(request.GroupId, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            if (schedule.Groups.Any(g => g.Id != request.GroupId
                                         && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
                return BasicResponse.WithError(ScheduleGroupNameTaken);

            schedule.Groups.Single(g => g.Id == request.GroupId).Name = name;
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
