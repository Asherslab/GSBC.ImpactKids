using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>The group goes, and so does every lane's mention of it. The lanes and their items stay.</summary>
    public async Task<BasicResponse> RemoveGroup(BasicReadRequest request, CallContext context = default)
    {
        Guid? scheduleId = await ScheduleOfGroup(request.Guid, context.CancellationToken);

        return await EditAsync(scheduleId, (schedule, _) =>
        {
            DbScheduleGroup group = schedule.Groups.Single(g => g.Id == request.Guid);

            foreach (DbScheduleLane lane in schedule.Blocks.SelectMany(b => b.Lanes))
            {
                // Reassigned rather than mutated in place: EF notices a primitive collection
                // changing by comparing it with a snapshot, and a new list is unambiguous.
                if (lane.GroupIds.Contains(group.Id))
                    lane.GroupIds = lane.GroupIds.Where(id => id != group.Id).ToList();
            }

            schedule.Groups.Remove(group);
            db.ScheduleGroups.Remove(group);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
